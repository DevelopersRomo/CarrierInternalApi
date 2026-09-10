using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace InternalCarrierApp.API.Filtering;

public enum TableFieldType
{
    String,
    Integer,
    Date,
    DateTime
}

public sealed record TableFilterCondition(string Field, string Operator, object? Value);

public sealed class TableQueryValidationException(string message) : Exception(message);

public sealed class TableQueryField<T>(string name, TableFieldType type, LambdaExpression selector, bool searchable)
{
    public string Name { get; } = name;
    public TableFieldType Type { get; } = type;
    public LambdaExpression Selector { get; } = selector;
    public bool Searchable { get; } = searchable;
    public bool CanBeNull { get; } = !selector.ReturnType.IsValueType || Nullable.GetUnderlyingType(selector.ReturnType) is not null;

    public static TableQueryField<T> String(
        string name,
        Expression<Func<T, string?>> selector,
        bool searchable = true) => new(name, TableFieldType.String, selector, searchable);

    public static TableQueryField<T> Integer(
        string name,
        Expression<Func<T, int>> selector,
        bool searchable = false) => new(name, TableFieldType.Integer, selector, searchable);

    public static TableQueryField<T> NullableInteger(
        string name,
        Expression<Func<T, int?>> selector,
        bool searchable = false) => new(name, TableFieldType.Integer, selector, searchable);

    public static TableQueryField<T> Date(
        string name,
        Expression<Func<T, DateOnly?>> selector) => new(name, TableFieldType.Date, selector, false);

    public static TableQueryField<T> DateTime(
        string name,
        Expression<Func<T, System.DateTime>> selector) => new(name, TableFieldType.DateTime, selector, false);
}

public static partial class TableQuery
{
    private static readonly HashSet<string> StringOperators =
        ["eq", "ne", "ilike", "not_ilike", "in", "not_in", "is_null", "is_not_null"];

    private static readonly HashSet<string> ComparableOperators =
        ["eq", "ne", "gt", "ge", "lt", "le", "in", "not_in", "between", "is_null", "is_not_null"];

    public static IReadOnlyList<TableFilterCondition> ParseFilters<T>(
        string? filterQuery,
        IReadOnlyDictionary<string, TableQueryField<T>> fields)
    {
        if (string.IsNullOrWhiteSpace(filterQuery))
            return [];

        var conditions = new List<TableFilterCondition>();
        foreach (var rawCondition in SplitConditions(filterQuery))
        {
            var match = ConditionPattern().Match(rawCondition);
            if (!match.Success)
                throw new TableQueryValidationException($"Malformed filter condition '{rawCondition}'.");

            var fieldName = match.Groups["field"].Value;
            var operation = match.Groups["operator"].Value.ToLowerInvariant();
            var rawValue = match.Groups["value"].Success ? match.Groups["value"].Value.Trim() : null;

            if (fieldName.Equals("global_search", StringComparison.OrdinalIgnoreCase))
            {
                if (operation != "eq" || rawValue is null)
                    throw new TableQueryValidationException("global_search only supports eq with a quoted value.");

                conditions.Add(new TableFilterCondition("global_search", "eq", ParseQuotedString(rawValue)));
                continue;
            }

            if (!fields.TryGetValue(fieldName, out var field))
                throw new TableQueryValidationException($"Unknown filter field '{fieldName}'.");

            var allowedOperators = field.Type == TableFieldType.String ? StringOperators : ComparableOperators;
            if (!allowedOperators.Contains(operation))
                throw new TableQueryValidationException(
                    $"Operator '{operation}' is not supported for field '{field.Name}'.");

            var isNullOperator = operation is "is_null" or "is_not_null";
            if (isNullOperator)
            {
                if (!field.CanBeNull)
                    throw new TableQueryValidationException(
                        $"Operator '{operation}' is not supported for non-nullable field '{field.Name}'.");
                if (rawValue is not null)
                    throw new TableQueryValidationException($"Operator '{operation}' does not accept a value.");
                conditions.Add(new TableFilterCondition(field.Name, operation, null));
                continue;
            }

            if (rawValue is null)
                throw new TableQueryValidationException($"Operator '{operation}' requires a value.");

            var expectsList = operation is "in" or "not_in" or "between";
            object value = expectsList
                ? ParseList(rawValue).Select(item => ConvertValue(item, field)).ToArray()
                : ConvertValue(ParseScalar(rawValue), field);

            if (operation == "between" && ((object[])value).Length != 2)
                throw new TableQueryValidationException(
                    $"Operator 'between' requires exactly two values for field '{field.Name}'.");

            conditions.Add(new TableFilterCondition(field.Name, operation, value));
        }

        return conditions;
    }

    public static IQueryable<T> ApplyFilters<T>(
        IQueryable<T> query,
        string? filterQuery,
        IReadOnlyDictionary<string, TableQueryField<T>> fields)
    {
        foreach (var condition in ParseFilters(filterQuery, fields))
        {
            if (condition.Field == "global_search")
            {
                var parameter = Expression.Parameter(typeof(T), "record");
                Expression? combined = null;
                foreach (var field in fields.Values.Where(field => field.Searchable && field.Type == TableFieldType.String))
                {
                    var searchCondition = new TableFilterCondition(field.Name, "ilike", condition.Value);
                    var predicate = BuildPredicate(field, searchCondition, parameter);
                    combined = combined is null ? predicate : Expression.OrElse(combined, predicate);
                }

                if (combined is not null)
                    query = query.Where(Expression.Lambda<Func<T, bool>>(combined, parameter));
                continue;
            }

            query = query.Where(BuildPredicateLambda(fields[condition.Field], condition));
        }

        return query;
    }

    public static IOrderedQueryable<T> ApplySort<T>(
        IQueryable<T> query,
        string? sortBy,
        IReadOnlyDictionary<string, TableQueryField<T>> fields,
        string defaultField,
        string defaultDirection = "asc")
    {
        var (fieldName, direction) = ParseSort(sortBy, fields, defaultField, defaultDirection);
        var field = fields[fieldName];
        var method = direction == "desc" ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
        var call = Expression.Call(
            typeof(Queryable),
            method,
            [typeof(T), field.Selector.ReturnType],
            query.Expression,
            Expression.Quote(field.Selector));

        return (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(call);
    }

    public static (string Field, string Direction) ParseSort<T>(
        string? sortBy,
        IReadOnlyDictionary<string, TableQueryField<T>> fields,
        string defaultField,
        string defaultDirection = "asc")
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return (defaultField, defaultDirection);

        var parts = sortBy.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2 || !fields.ContainsKey(parts[0]))
            throw new TableQueryValidationException($"Unknown sort field '{parts.FirstOrDefault() ?? sortBy}'.");

        var direction = parts.Length == 2 ? parts[1].ToLowerInvariant() : "asc";
        if (direction is not ("asc" or "desc"))
            throw new TableQueryValidationException("Sort direction must be 'asc' or 'desc'.");

        return (fields[parts[0]].Name, direction);
    }

    private static Expression<Func<T, bool>> BuildPredicateLambda<T>(
        TableQueryField<T> field,
        TableFilterCondition condition)
    {
        var parameter = Expression.Parameter(typeof(T), "record");
        return Expression.Lambda<Func<T, bool>>(BuildPredicate(field, condition, parameter), parameter);
    }

    private static Expression BuildPredicate<T>(
        TableQueryField<T> field,
        TableFilterCondition condition,
        ParameterExpression parameter)
    {
        var property = new ReplaceParameterVisitor(field.Selector.Parameters[0], parameter)
            .Visit(field.Selector.Body)!;

        if (condition.Operator == "is_null")
            return Expression.Equal(property, Expression.Constant(null, property.Type));
        if (condition.Operator == "is_not_null")
            return Expression.NotEqual(property, Expression.Constant(null, property.Type));

        if (condition.Operator is "in" or "not_in")
        {
            var values = (object[])condition.Value!;
            Expression? matches = null;
            foreach (var value in values)
            {
                var equality = Expression.Equal(property, ConstantFor(property.Type, value));
                matches = matches is null ? equality : Expression.OrElse(matches, equality);
            }

            matches ??= Expression.Constant(false);
            return condition.Operator == "not_in" ? Expression.Not(matches) : matches;
        }

        if (condition.Operator == "between")
        {
            var values = (object[])condition.Value!;
            return Expression.AndAlso(
                Expression.GreaterThanOrEqual(property, ConstantFor(property.Type, values[0])),
                Expression.LessThanOrEqual(property, ConstantFor(property.Type, values[1])));
        }

        var constant = ConstantFor(property.Type, condition.Value!);
        if (condition.Operator is "ilike" or "not_ilike")
        {
            var notNull = Expression.NotEqual(property, Expression.Constant(null, property.Type));
            var loweredProperty = Expression.Call(property, nameof(string.ToLower), Type.EmptyTypes);
            var loweredValue = Expression.Constant(((string)condition.Value!).ToLowerInvariant());
            var contains = Expression.Call(loweredProperty, nameof(string.Contains), Type.EmptyTypes, loweredValue);
            var match = Expression.AndAlso(notNull, contains);
            return condition.Operator == "not_ilike" ? Expression.Not(match) : match;
        }

        return condition.Operator switch
        {
            "eq" => Expression.Equal(property, constant),
            "ne" => Expression.NotEqual(property, constant),
            "gt" => Expression.GreaterThan(property, constant),
            "ge" => Expression.GreaterThanOrEqual(property, constant),
            "lt" => Expression.LessThan(property, constant),
            "le" => Expression.LessThanOrEqual(property, constant),
            _ => throw new TableQueryValidationException($"Unsupported operator '{condition.Operator}'.")
        };
    }

    private static Expression ConstantFor(Type propertyType, object value)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyType);
        var constant = Expression.Constant(value, underlyingType ?? propertyType);
        return underlyingType is null ? constant : Expression.Convert(constant, propertyType);
    }

    private static object ConvertValue<T>(string value, TableQueryField<T> field)
    {
        return field.Type switch
        {
            TableFieldType.String => value,
            TableFieldType.Integer when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            TableFieldType.Date when DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) => date,
            TableFieldType.DateTime when System.DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime) => dateTime,
            _ => throw new TableQueryValidationException($"Invalid value '{value}' for field '{field.Name}'.")
        };
    }

    private static IReadOnlyList<string> SplitConditions(string query)
    {
        var conditions = new List<string>();
        var start = 0;
        var inQuote = false;
        var bracketDepth = 0;

        for (var index = 0; index < query.Length; index++)
        {
            if (query[index] == '\'' && inQuote && index + 1 < query.Length && query[index + 1] == '\'')
            {
                index++;
                continue;
            }

            if (query[index] == '\'')
                inQuote = !inQuote;
            else if (!inQuote && query[index] == '[')
                bracketDepth++;
            else if (!inQuote && query[index] == ']')
                bracketDepth--;

            if (!inQuote && bracketDepth == 0 && IsAndSeparator(query, index))
            {
                AddCondition(query[start..index], conditions);
                index += 4;
                start = index + 1;
            }
        }

        if (inQuote || bracketDepth != 0)
            throw new TableQueryValidationException("Malformed quoted value or list in filter_query.");

        AddCondition(query[start..], conditions);
        return conditions;
    }

    private static bool IsAndSeparator(string query, int index) =>
        index > 0 && index + 4 < query.Length &&
        char.IsWhiteSpace(query[index]) &&
        query.AsSpan(index + 1, 3).Equals("AND", StringComparison.OrdinalIgnoreCase) &&
        char.IsWhiteSpace(query[index + 4]);

    private static void AddCondition(string value, ICollection<string> conditions)
    {
        var condition = value.Trim();
        if (condition.Length == 0)
            throw new TableQueryValidationException("filter_query contains an empty condition.");
        conditions.Add(condition);
    }

    private static string ParseScalar(string rawValue)
    {
        if (rawValue.StartsWith('\''))
            return ParseQuotedString(rawValue);
        if (Regex.IsMatch(rawValue, @"^-?\d+(?:\.\d+)?$"))
            return rawValue;
        throw new TableQueryValidationException($"Malformed value '{rawValue}'. Strings must be quoted.");
    }

    private static string ParseQuotedString(string rawValue)
    {
        if (rawValue.Length < 2 || rawValue[0] != '\'' || rawValue[^1] != '\'')
            throw new TableQueryValidationException($"Malformed quoted value '{rawValue}'.");

        var result = new StringBuilder();
        for (var index = 1; index < rawValue.Length - 1; index++)
        {
            if (rawValue[index] == '\'')
            {
                if (index + 1 >= rawValue.Length - 1 || rawValue[index + 1] != '\'')
                    throw new TableQueryValidationException($"Malformed quoted value '{rawValue}'.");
                result.Append('\'');
                index++;
            }
            else
            {
                result.Append(rawValue[index]);
            }
        }

        return result.ToString();
    }

    private static IReadOnlyList<string> ParseList(string rawValue)
    {
        if (rawValue.Length < 2 || rawValue[0] != '[' || rawValue[^1] != ']')
            throw new TableQueryValidationException("List values must be enclosed in square brackets.");

        var inner = rawValue[1..^1].Trim();
        if (inner.Length == 0)
            return [];

        var values = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;
        for (var index = 0; index < inner.Length; index++)
        {
            var character = inner[index];
            if (character == '\'' && inQuote && index + 1 < inner.Length && inner[index + 1] == '\'')
            {
                current.Append("''");
                index++;
                continue;
            }

            if (character == '\'')
                inQuote = !inQuote;

            if (character == ',' && !inQuote)
            {
                values.Add(ParseScalar(current.ToString().Trim()));
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        if (inQuote)
            throw new TableQueryValidationException("Malformed quoted value in list.");

        values.Add(ParseScalar(current.ToString().Trim()));
        return values;
    }

    [GeneratedRegex(@"^(?<field>[A-Za-z][A-Za-z0-9_]*)\s+(?<operator>[A-Za-z_]+)(?:\s+(?<value>.+))?$")]
    private static partial Regex ConditionPattern();

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : base.VisitParameter(node);
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int Size, int Pages);

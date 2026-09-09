# Novu workflows

Workflow definitions live here as JSON and are pushed to Novu with a script, so the
repository is the source of truth and the dashboard is the mirror. Editing a workflow
by hand in the dashboard works, but the next sync overwrites it.

## Layout

```
Novu/
  workflows/<workflow-id>.json   one file per workflow; the file name IS the workflowId
  sync-workflows.sh              create or update workflows in one environment
  promote-workflows.sh           copy workflows from Development to another environment
```

## Sync

```bash
NOVU_SECRET_KEY=<key> ./Novu/sync-workflows.sh
```

Pass names to sync a subset:

```bash
NOVU_SECRET_KEY=<key> ./Novu/sync-workflows.sh carrier-notification
```

The secret key is environment-scoped: whichever environment it belongs to is the one
that gets written. Take it from the dashboard under **Settings → API Keys**.

## Promote to production

Workflows are authored in Development and copied over, never edited directly in
production:

```bash
NOVU_SECRET_KEY=<development-key> \
NOVU_TARGET_ENVIRONMENT_ID=<production-environment-id> \
  ./Novu/promote-workflows.sh
```

## Payload contract

Every workflow reads the same three fields, so a new notification can be written
without opening the dashboard to look up variable names:

| Field          | Required | Purpose                                                |
| -------------- | -------- | ------------------------------------------------------ |
| `title`        | yes      | Inbox subject and email subject                        |
| `description`  | yes      | Body text                                              |
| `redirect_url` | no       | Application path the notification opens, e.g. `/servers/42` |

`validatePayload` is enabled, so Novu rejects a trigger whose payload is missing a
required field instead of delivering an empty notification.

From the API:

```csharp
await novu.TriggerAsync(
    "carrier-notification",
    new NovuRecipient(user.Id, user.Email, user.FullName),
    new { title = "Server down", description = $"{server.ServerName} is not responding", redirect_url = $"/servers/{server.Id}" });
```

`NovuService` serialises payload keys with a camelCase policy, so both
`new { redirect_url = ... }` and a record with `RedirectUrl` reach the template as a
key the Liquid expressions can find.

## Adding a workflow

1. Copy `workflows/carrier-notification.json` to `workflows/<new-id>.json`.
2. Set `name` and `workflowId` to the new id (it must match the file name).
3. Adjust the steps. Step types are `in_app`, `email`, `sms`, `push`, `chat`,
   `digest`, `delay`, `throttle`, `custom`, `http_request`, `tool`.
4. Run the sync script.

Note that `active` defaults to `false` in the Novu API. A workflow without
`"active": true` is created but never fires.

## Email delivery

The in-app step works out of the box. Email needs a real provider configured under
**Integrations → Email** in the dashboard; the default sandbox provider only delivers
to the address of the Novu account itself.

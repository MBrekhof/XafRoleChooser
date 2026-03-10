# Session Handoff

**Read this file first when starting a new session.**

Also read: `TODO.md`, `CLAUDE.md`, `docs/plans/2026-03-10-role-chooser-design.md`

## Project State

Building a **reusable XAF module** that lets users choose which roles are active after login via a toolbar multi-select dropdown. "Default" role is always active. No restart needed.

## Current Status

**Phase: Design complete, implementation not started.**

Design approved and saved to `docs/plans/2026-03-10-role-chooser-design.md`.

## Key Decisions Made

1. **Reusable module** — standalone NuGet-style package, not tied to the demo app
2. **Multi-select dropdown with Apply** — not individual toggles, not role profiles
3. **No role dependencies** — each role is an independent toggle
4. **No restart** — uses `PermissionsReloadMode.NoCache` for live permission updates
5. **Flat structure** — new module project alongside existing demo projects
6. **Platform projects deferred** — start module-only, add Blazor/Win if needed

## Architecture Quick Reference

| Component | Purpose |
|---|---|
| `IActiveRoleFilter` | Interface: get/set active role IDs per session |
| `ActiveRoleFilter` | Scoped implementation |
| `ActiveRoleSelection` | NonPersistent BO for checklist UI |
| `RoleChooserController` | WindowController — toolbar action + popup |
| `RoleChooserSecurityFilter` | Filters roles at permission evaluation |
| `XafRoleChooserModule` | Module class, self-registers everything |

## Next Steps

Start Phase 1: create the module project and implement core components. See `TODO.md` for full checklist.

# TODO — XAF Role Chooser

## Phase 1: Core Module
- [ ] Create `XafRoleChooser.Module` project (net8.0, DevExpress.ExpressApp + Security refs)
- [ ] Add to solution `XafRoleChooser.slnx`
- [ ] Implement `IActiveRoleFilter` interface
- [ ] Implement `ActiveRoleFilter` (scoped, stores active role IDs)
- [ ] Implement `ActiveRoleSelection` NonPersistent BO (role name, IsActive checkbox)
- [ ] Implement `RoleChooserController` (WindowController, toolbar action, popup with multi-select)
- [ ] Implement `RoleChooserSecurityFilter` (intercept permission evaluation, filter roles)
- [ ] Implement `XafRoleChooserModule` class (service registration, type export, config properties)

## Phase 2: Demo App Integration
- [ ] Reference new module from existing demo `Demo.Module`
- [ ] Register module in Blazor `Startup.cs`
- [ ] Register module in Win `Startup.cs`
- [ ] Add additional test roles to `Updater.cs` seed data for demo purposes
- [ ] Verify toolbar dropdown appears after login
- [ ] Verify permission changes take effect without restart

## Phase 3: Documentation
- [ ] Write `docs/how-to-implement.md` — step-by-step guide for XAF developers
- [ ] Update `CLAUDE.md` with new module structure

## Open Questions
- Exact interception point for `RoleChooserSecurityFilter` — need to verify best XAF API (`CustomizeRequestProcessors` vs wrapping `ISelectDataSecurityProvider`)
- Whether platform-specific projects (Blazor/Win) are needed for the toolbar UI or if WindowController works cross-platform

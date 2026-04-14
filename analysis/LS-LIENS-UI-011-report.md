# LS-LIENS-UI-011: Provider Mode (Sell vs Manage Internally)

## Feature ID
LS-LIENS-UI-011

## Objective
Enable dynamic product behavior based on provider mode (Sell vs Manage Internally). Sell mode exposes marketplace, offers, bill-of-sale features. Manage mode restricts to internal lien management only.

## Mode Assumptions
- Mode is a tenant/org-level configuration, not per-user
- Two modes: `sell` and `manage`
- `sell` = full feature set (marketplace, offers, BOS, settlements)
- `manage` = internal lien tracking only (no marketplace, no offers, no BOS)
- Mode should come from session/org configuration

## Expected API/Config Source
- TBD — analyzing session, tenant config, and org settings

## Status: T001 — Initial Analysis IN PROGRESS

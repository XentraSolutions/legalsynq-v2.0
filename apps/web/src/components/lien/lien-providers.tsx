'use client';

import { type ReactNode } from 'react';
import { ToastContainer } from './toast-container';
// RoleSwitcher (Simulate Role): will not implement for phase 1 — hidden for now.
// import { RoleSwitcher } from './role-switcher';

export function LienProviders({ children }: { children: ReactNode }) {
  return (
    <>
      {children}
      <ToastContainer />
      {/* <RoleSwitcher /> */}
    </>
  );
}

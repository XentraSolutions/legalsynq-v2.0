import { Request, Response, NextFunction } from "express";

const TENANT_HEADER = "x-tenant-id";
const DEFAULT_TENANT_ID = "default";

export function tenantMiddleware(req: Request, _res: Response, next: NextFunction): void {
  const tenantId = req.headers[TENANT_HEADER];

  if (Array.isArray(tenantId)) {
    req.tenantId = tenantId[0] ?? DEFAULT_TENANT_ID;
  } else {
    req.tenantId = tenantId ?? DEFAULT_TENANT_ID;
  }

  next();
}

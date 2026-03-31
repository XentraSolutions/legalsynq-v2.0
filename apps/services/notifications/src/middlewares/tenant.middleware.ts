import { Request, Response, NextFunction } from "express";

const TENANT_HEADER = "x-tenant-id";

export function tenantMiddleware(req: Request, res: Response, next: NextFunction): void {
  if (req.path === "/v1/health" || req.path.startsWith("/v1/health/")) {
    return next();
  }

  const raw = req.headers[TENANT_HEADER];
  const tenantId = Array.isArray(raw) ? raw[0] : raw;

  if (!tenantId || tenantId.trim() === "") {
    res.status(400).json({
      error: {
        code: "MISSING_TENANT_CONTEXT",
        message: "x-tenant-id header is required",
      },
    });
    return;
  }

  req.tenantId = tenantId.trim();
  next();
}

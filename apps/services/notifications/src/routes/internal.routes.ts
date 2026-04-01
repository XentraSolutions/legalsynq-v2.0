import { Router, Request, Response } from "express";
import { TenantProviderConfig } from "../models/tenant-provider-config.model";
import { SendGridEmailProviderAdapter } from "../integrations/providers/adapters/sendgrid.adapter";
import { decrypt } from "../shared/crypto.service";
import { logger } from "../shared/logger";

const router = Router();

/**
 * POST /internal/send-email
 * Platform-internal endpoint used by CareConnect (and any other service) to dispatch
 * a transactional email via the platform-level SendGrid provider configured in Control Center.
 *
 * Body: { to: string, subject: string, htmlBody: string }
 * Returns: 200 on success, 503 if no platform provider configured, 500 on failure.
 *
 * No x-tenant-id header required — this is always platform-level.
 */
router.post("/send-email", async (req: Request, res: Response): Promise<void> => {
  const { to, subject, htmlBody } = req.body as { to?: string; subject?: string; htmlBody?: string };

  if (!to || !subject || !htmlBody) {
    res.status(400).json({ error: "Missing required fields: to, subject, htmlBody" });
    return;
  }

  const config = await TenantProviderConfig.findOne({
    where: {
      tenantId: null,
      channel: "email",
      providerType: "sendgrid",
      status: "active",
    },
    order: [["createdAt", "DESC"]],
  });

  if (!config) {
    logger.warn("Internal send-email: no active platform-level SendGrid config found");
    res.status(503).json({ error: "No active platform email provider configured. Set one up in Control Center." });
    return;
  }

  const endpointConfig = config.endpointConfigJson
    ? (JSON.parse(config.endpointConfigJson) as Record<string, unknown>)
    : {};

  if (!config.credentialReference) {
    logger.warn("Internal send-email: platform SendGrid config has no credentials", { configId: config.id });
    res.status(503).json({ error: "Platform email provider has no credentials configured." });
    return;
  }

  let credentials: Record<string, unknown>;
  try {
    credentials = JSON.parse(decrypt(config.credentialReference)) as Record<string, unknown>;
  } catch (err) {
    logger.error("Internal send-email: failed to decrypt credentials", { configId: config.id, error: String(err) });
    res.status(500).json({ error: "Failed to decrypt provider credentials." });
    return;
  }

  const fromEmail = endpointConfig["fromEmail"] as string;
  const fromName  = (endpointConfig["fromName"] as string) ?? "";

  if (!fromEmail) {
    logger.warn("Internal send-email: endpointConfig missing fromEmail", { configId: config.id });
    res.status(503).json({ error: "Platform email provider is missing a From address." });
    return;
  }

  const apiKey = credentials["apiKey"] as string;
  if (!apiKey) {
    logger.warn("Internal send-email: credentials missing apiKey", { configId: config.id });
    res.status(503).json({ error: "Platform email provider is missing an API key." });
    return;
  }

  const adapter = new SendGridEmailProviderAdapter({
    apiKey,
    defaultFromEmail: fromEmail,
    defaultFromName:  fromName,
  });

  const from = fromName ? `${fromName} <${fromEmail}>` : fromEmail;

  try {
    const result = await adapter.send({ to, from, subject, body: htmlBody });
    if (result.success) {
      logger.info("Internal send-email: sent successfully", { to, configId: config.id });
      res.status(200).json({ success: true });
    } else {
      logger.warn("Internal send-email: provider rejected send", {
        to,
        configId: config.id,
        failure: result.failure?.message,
      });
      res.status(500).json({ error: result.failure?.message ?? "Provider rejected the send request." });
    }
  } catch (err) {
    logger.error("Internal send-email: unexpected error", { to, error: String(err) });
    res.status(500).json({ error: "Unexpected error while sending email." });
  }
});

export default router;

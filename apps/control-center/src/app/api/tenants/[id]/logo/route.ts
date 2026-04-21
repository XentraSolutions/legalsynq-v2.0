import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';

const GATEWAY_URL = process.env.GATEWAY_URL ?? process.env.CONTROL_CENTER_API_BASE ?? 'http://127.0.0.1:5010';
const TENANT_LOGO_DOC_TYPE = '20000000-0000-0000-0000-000000000002';

function parseJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    return JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf-8'));
  } catch {
    return null;
  }
}

/**
 * POST /api/tenants/[id]/logo — upload a new logo for the tenant.
 *
 * Flow:
 *   1. Upload the image to the Documents service (referenceType: "Tenant").
 *   2. Persist the returned document ID on the tenant record via
 *      PATCH /identity/api/admin/tenants/{id}/logo.
 */
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value;
  if (!token) return NextResponse.json({ error: 'UNAUTHENTICATED' }, { status: 401 });

  const payload = parseJwtPayload(token);
  if (!payload) return NextResponse.json({ error: 'INVALID_TOKEN' }, { status: 401 });

  const tenantId = payload['tenant_id'] as string;
  if (!tenantId) return NextResponse.json({ error: 'INVALID_TOKEN_CLAIMS' }, { status: 401 });

  if (!req.headers.get('content-type')?.includes('multipart/form-data'))
    return NextResponse.json({ error: 'INVALID_CONTENT_TYPE' }, { status: 400 });

  const { id: targetTenantId } = await params;
  const formData = await req.formData();
  const file = formData.get('file') as File | null;
  if (!file || file.size === 0)
    return NextResponse.json({ error: 'FILE_REQUIRED' }, { status: 400 });

  const uploadForm = new FormData();
  uploadForm.append('tenantId',       targetTenantId);
  uploadForm.append('documentTypeId', TENANT_LOGO_DOC_TYPE);
  uploadForm.append('productId',      'identity');
  uploadForm.append('referenceId',    targetTenantId);
  uploadForm.append('referenceType',  'Tenant');
  uploadForm.append('title',          'Tenant Logo');
  uploadForm.append('file',           file, file.name || 'logo');

  const docsRes = await fetch(`${GATEWAY_URL}/documents/documents`, {
    method:  'POST',
    headers: {
      Authorization:           `Bearer ${token}`,
      'X-Admin-Target-Tenant': targetTenantId,
    },
    body: uploadForm,
  });

  if (!docsRes.ok) {
    const err = await docsRes.text();
    return NextResponse.json({ error: 'UPLOAD_FAILED', detail: err }, { status: docsRes.status });
  }

  const { data } = (await docsRes.json()) as { data: { id: string } };
  const docId    = data.id;

  const patchRes = await fetch(`${GATEWAY_URL}/identity/api/admin/tenants/${targetTenantId}/logo`, {
    method:  'PATCH',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body:    JSON.stringify({ documentId: docId }),
  });

  if (!patchRes.ok) {
    const err = await patchRes.text();
    return NextResponse.json({ error: 'LOGO_UPDATE_FAILED', detail: err }, { status: patchRes.status });
  }

  // Register the document as the active published logo in the Documents service.
  // This is required so that the anonymous /public/logo/{id} endpoint will serve it.
  const regRes = await fetch(
    `${GATEWAY_URL}/documents/documents/${docId}/logo-registration`,
    {
      method:  'PUT',
      headers: {
        Authorization:           `Bearer ${token}`,
        'X-Admin-Target-Tenant': targetTenantId,
      },
    },
  );

  if (!regRes.ok) {
    const err = await regRes.text();
    return NextResponse.json({ error: 'LOGO_REGISTRATION_FAILED', detail: err }, { status: regRes.status });
  }

  return NextResponse.json({ logoDocumentId: docId });
}

/**
 * DELETE /api/tenants/[id]/logo — remove the tenant logo.
 */
export async function DELETE(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value;
  if (!token) return NextResponse.json({ error: 'UNAUTHENTICATED' }, { status: 401 });

  const { id: targetTenantId } = await params;

  const delRes = await fetch(`${GATEWAY_URL}/identity/api/admin/tenants/${targetTenantId}/logo`, {
    method:  'DELETE',
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!delRes.ok) {
    const err = await delRes.text();
    return NextResponse.json({ error: 'DELETE_FAILED', detail: err }, { status: delRes.status });
  }

  // Clear all logo registrations for the tenant in the Documents service so the
  // anonymous /public/logo/{id} endpoint no longer serves the old logo.
  // X-Admin-Target-Tenant ensures the Documents service clears the correct
  // tenant's registrations when a platform-admin operates on another tenant.
  await fetch(`${GATEWAY_URL}/documents/documents/logo-registration`, {
    method:  'DELETE',
    headers: {
      Authorization:           `Bearer ${token}`,
      'X-Admin-Target-Tenant': targetTenantId,
    },
  });
  // Ignore errors on the logo deregistration to avoid breaking the delete flow
  // if the Documents service is temporarily unavailable.

  return NextResponse.json({ ok: true });
}

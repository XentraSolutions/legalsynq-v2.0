'use client';

import { useState, useEffect, useCallback } from 'react';
import { useLienStore } from '@/stores/lien-store';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { MedicalFacilityStaffSection } from '@/components/lien/medical-facility-staff-section';
import { ContactCasesSection } from '@/components/lien/contact-cases-section';
import { EntityTimeline } from '@/components/lien/entity-timeline';
import { facilityService } from '@/lib/facility';
import type { LegacyFacilityItem } from '@/lib/facility/facility.types';

export function LegacyContactDetailClient({ id }: { id: string }) {
  const addToast = useLienStore((s) => s.addToast);
  const [facility, setFacility] = useState<LegacyFacilityItem | null>(null);
  const [loading, setLoading] = useState(true);

  const fetchFacility = useCallback(async () => {
    try {
      setLoading(true);
      const data = await facilityService.getFacility(id);
      setFacility(data);
    } catch (err) {
      addToast({ type: 'error', title: 'Load Failed', description: err instanceof Error ? err.message : 'Failed to load facility' });
    } finally {
      setLoading(false);
    }
  }, [id, addToast]);

  useEffect(() => { fetchFacility(); }, [fetchFacility]);

  if (loading) return <div className="p-10 text-center text-sm text-gray-400">Loading facility...</div>;
  if (!facility) return <div className="p-10 text-center text-gray-400">Facility not found.</div>;

  return (
    <div className="space-y-5">
      <DetailHeader
        title={facility.name}
        backHref="/lien/contacts?type=MedicalFacility"
        backLabel="Back to Contacts"
        badge={
          <span className="inline-flex items-center rounded-full border px-2.5 py-1 text-sm font-medium bg-gray-50 text-gray-600 border-gray-200">
            Medical Facility
          </span>
        }
        meta={[
          { label: 'Status', value: facility.isActive ? 'Active' : 'Inactive' },
          { label: 'Member Since', value: facility.createdAtUtc },
        ]}
      />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="Contact Information" icon="ri-contacts-book-line" fields={[
          { label: 'Email', value: facility.email },
          { label: 'Phone', value: facility.phone },
        ]} />
        <DetailSection title="Location" icon="ri-map-pin-line" fields={[
          { label: 'Address', value: facility.addressLine1 },
          { label: 'City', value: facility.city },
          { label: 'State', value: facility.state },
          { label: 'ZIP Code', value: facility.postalCode },
        ]} />
      </div>

      <MedicalFacilityStaffSection facilityId={id} />

      <ContactCasesSection contactId={id} contactType="MedicalFacility" />

      <EntityTimeline entityType="Contact" entityId={id} />
    </div>
  );
}

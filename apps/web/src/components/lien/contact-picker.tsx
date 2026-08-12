'use client';

import { useEffect, useMemo, useState } from 'react';
import { useContacts } from '@/hooks/use-contacts';
import { Combobox } from '@/components/ui/combobox';

interface ContactPickerProps {
  contactType: string;
  // Sub-contact role code (e.g. resolved case-manager code, or
  // "FacilityContactPerson"). Defaults to "" — main contacts only, matching
  // the rest of the app's convention for excluding sub-contacts.
  contactSubtype?: string;
  lawFirmId?: string;
  facilityId?: string;
  excludeId?: string;
  value: string;
  onChange: (id: string) => void;
  placeholder?: string;
  disabled?: boolean;
  error?: boolean;
}

export function ContactPicker({
  contactType,
  contactSubtype = '',
  lawFirmId,
  facilityId,
  excludeId,
  value,
  onChange,
  placeholder,
  disabled,
  error,
}: ContactPickerProps) {
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(timeout);
  }, [search]);

  const query = useContacts({
    ContactType: contactType,
    ContactSubtype: contactSubtype,
    LawFirmId: lawFirmId,
    FacilityId: facilityId,
    search: debouncedSearch || undefined,
    pageSize: 25,
  });

  const options = useMemo(() => {
    const items = query.data?.items ?? [];
    return items
      .filter((c) => c.id !== excludeId)
      .map((c) => ({ value: c.id, label: c.displayName }));
  }, [query.data, excludeId]);

  return (
    <Combobox
      value={value}
      onChange={onChange}
      onSearchChange={setSearch}
      options={options}
      placeholder={placeholder ?? 'Select contact...'}
      searchPlaceholder="Search contacts..."
      emptyText={query.isFetching ? 'Searching...' : 'No contacts found.'}
      disabled={disabled}
      error={error}
    />
  );
}

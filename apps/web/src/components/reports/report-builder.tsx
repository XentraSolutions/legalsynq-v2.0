'use client';

import { useState, useCallback } from 'react';
import type { ColumnConfig, FilterRule } from '@/lib/reports/reports.types';

interface ReportBuilderProps {
  availableFields: ColumnConfig[];
  initialColumns: ColumnConfig[];
  initialFilters: FilterRule[];
  onSave: (columns: ColumnConfig[], filters: FilterRule[]) => Promise<void>;
  onCancel: () => void;
}

const OPERATORS = [
  { value: 'equals', label: 'Equals' },
  { value: 'contains', label: 'Contains' },
  { value: 'greaterThan', label: 'Greater Than' },
  { value: 'lessThan', label: 'Less Than' },
  { value: 'between', label: 'Between' },
  { value: 'in', label: 'In' },
] as const;

export function ReportBuilder({
  availableFields,
  initialColumns,
  initialFilters,
  onSave,
  onCancel,
}: ReportBuilderProps) {
  const [selectedCols, setSelectedCols] = useState<ColumnConfig[]>(
    initialColumns.length > 0 ? initialColumns : [],
  );
  const [filters, setFilters] = useState<FilterRule[]>(initialFilters);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const unselected = availableFields.filter(
    (f) => !selectedCols.some((s) => s.name === f.name),
  );

  const addColumn = useCallback((field: ColumnConfig) => {
    setSelectedCols((prev) => [
      ...prev,
      { ...field, order: prev.length, visible: true },
    ]);
  }, []);

  const removeColumn = useCallback((name: string) => {
    setSelectedCols((prev) =>
      prev.filter((c) => c.name !== name).map((c, i) => ({ ...c, order: i })),
    );
  }, []);

  const moveColumn = useCallback((index: number, dir: -1 | 1) => {
    setSelectedCols((prev) => {
      const next = [...prev];
      const target = index + dir;
      if (target < 0 || target >= next.length) return prev;
      [next[index], next[target]] = [next[target], next[index]];
      return next.map((c, i) => ({ ...c, order: i }));
    });
  }, []);

  const renameColumn = useCallback((index: number, label: string) => {
    setSelectedCols((prev) =>
      prev.map((c, i) => (i === index ? { ...c, label } : c)),
    );
  }, []);

  const addFilter = useCallback(() => {
    if (availableFields.length === 0) return;
    setFilters((prev) => [
      ...prev,
      { field: availableFields[0].name, operator: 'equals', value: '' },
    ]);
  }, [availableFields]);

  const updateFilter = useCallback(
    (index: number, patch: Partial<FilterRule>) => {
      setFilters((prev) =>
        prev.map((f, i) => (i === index ? { ...f, ...patch } : f)),
      );
    },
    [],
  );

  const removeFilter = useCallback((index: number) => {
    setFilters((prev) => prev.filter((_, i) => i !== index));
  }, []);

  async function handleSave() {
    if (selectedCols.length === 0) {
      setError('Select at least one column.');
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await onSave(selectedCols, filters);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Save failed');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 lg:grid-cols-[300px_1fr] gap-6">
        <div className="border border-gray-200 rounded-lg">
          <div className="px-4 py-3 border-b border-gray-200 bg-gray-50">
            <h4 className="text-sm font-semibold text-gray-700">Available Fields</h4>
            <p className="text-xs text-gray-500 mt-0.5">{unselected.length} field{unselected.length !== 1 ? 's' : ''}</p>
          </div>
          <div className="p-2 max-h-[400px] overflow-y-auto space-y-1">
            {unselected.length === 0 ? (
              <p className="text-xs text-gray-400 px-2 py-3 text-center">All fields selected</p>
            ) : (
              unselected.map((f) => (
                <button
                  key={f.name}
                  onClick={() => addColumn(f)}
                  className="w-full flex items-center gap-2 px-3 py-2 text-sm text-gray-700 rounded-md hover:bg-gray-100 transition-colors text-left"
                >
                  <i className="ri-add-line text-gray-400" />
                  <span className="truncate">{f.label}</span>
                  <span className="text-xs text-gray-400 ml-auto">{f.dataType}</span>
                </button>
              ))
            )}
          </div>
        </div>

        <div className="border border-gray-200 rounded-lg">
          <div className="px-4 py-3 border-b border-gray-200 bg-gray-50">
            <h4 className="text-sm font-semibold text-gray-700">Selected Columns</h4>
            <p className="text-xs text-gray-500 mt-0.5">{selectedCols.length} column{selectedCols.length !== 1 ? 's' : ''}</p>
          </div>
          <div className="p-2 max-h-[400px] overflow-y-auto space-y-1">
            {selectedCols.length === 0 ? (
              <p className="text-xs text-gray-400 px-2 py-3 text-center">Add fields from the left panel</p>
            ) : (
              selectedCols.map((col, i) => (
                <div
                  key={col.name}
                  className="flex items-center gap-2 px-3 py-2 bg-white border border-gray-200 rounded-md"
                >
                  <span className="text-xs text-gray-400 w-5 shrink-0">{i + 1}</span>
                  <input
                    type="text"
                    value={col.label}
                    onChange={(e) => renameColumn(i, e.target.value)}
                    className="flex-1 text-sm text-gray-700 bg-transparent border-0 p-0 focus:ring-0 focus:outline-none"
                  />
                  <span className="text-xs text-gray-400">{col.dataType}</span>
                  <div className="flex items-center gap-0.5 ml-1">
                    <button
                      onClick={() => moveColumn(i, -1)}
                      disabled={i === 0}
                      className="p-1 text-gray-400 hover:text-gray-600 disabled:opacity-30"
                    >
                      <i className="ri-arrow-up-s-line text-sm" />
                    </button>
                    <button
                      onClick={() => moveColumn(i, 1)}
                      disabled={i === selectedCols.length - 1}
                      className="p-1 text-gray-400 hover:text-gray-600 disabled:opacity-30"
                    >
                      <i className="ri-arrow-down-s-line text-sm" />
                    </button>
                    <button
                      onClick={() => removeColumn(col.name)}
                      className="p-1 text-gray-400 hover:text-red-500"
                    >
                      <i className="ri-close-line text-sm" />
                    </button>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      <div className="border border-gray-200 rounded-lg">
        <div className="px-4 py-3 border-b border-gray-200 bg-gray-50 flex items-center justify-between">
          <div>
            <h4 className="text-sm font-semibold text-gray-700">Filters</h4>
            <p className="text-xs text-gray-500 mt-0.5">{filters.length} filter{filters.length !== 1 ? 's' : ''}</p>
          </div>
          <button
            onClick={addFilter}
            className="text-xs font-medium text-primary hover:text-primary/80 inline-flex items-center gap-1"
          >
            <i className="ri-add-line" />
            Add Filter
          </button>
        </div>
        <div className="p-3 space-y-2">
          {filters.length === 0 ? (
            <p className="text-xs text-gray-400 text-center py-3">No filters applied</p>
          ) : (
            filters.map((f, i) => (
              <div key={i} className="flex items-center gap-2 flex-wrap">
                <select
                  value={f.field}
                  onChange={(e) => updateFilter(i, { field: e.target.value })}
                  className="text-sm border border-gray-300 rounded-md px-2 py-1.5 bg-white"
                >
                  {availableFields.map((af) => (
                    <option key={af.name} value={af.name}>{af.label}</option>
                  ))}
                </select>
                <select
                  value={f.operator}
                  onChange={(e) => updateFilter(i, { operator: e.target.value as FilterRule['operator'] })}
                  className="text-sm border border-gray-300 rounded-md px-2 py-1.5 bg-white"
                >
                  {OPERATORS.map((op) => (
                    <option key={op.value} value={op.value}>{op.label}</option>
                  ))}
                </select>
                <input
                  type="text"
                  value={f.value}
                  onChange={(e) => updateFilter(i, { value: e.target.value })}
                  placeholder="Value"
                  className="text-sm border border-gray-300 rounded-md px-2 py-1.5 flex-1 min-w-[120px]"
                />
                {f.operator === 'between' && (
                  <input
                    type="text"
                    value={f.value2 ?? ''}
                    onChange={(e) => updateFilter(i, { value2: e.target.value })}
                    placeholder="End value"
                    className="text-sm border border-gray-300 rounded-md px-2 py-1.5 min-w-[120px]"
                  />
                )}
                <button
                  onClick={() => removeFilter(i)}
                  className="p-1.5 text-gray-400 hover:text-red-500"
                >
                  <i className="ri-delete-bin-line text-sm" />
                </button>
              </div>
            ))
          )}
        </div>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      <div className="flex items-center justify-end gap-3">
        <button
          onClick={onCancel}
          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
        >
          Cancel
        </button>
        <button
          onClick={handleSave}
          disabled={saving || selectedCols.length === 0}
          className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-50"
        >
          {saving ? 'Saving...' : 'Save Configuration'}
        </button>
      </div>
    </div>
  );
}

'use client';

import { useState } from 'react';

const AVAILABLE_COLUMNS = [
  'Plaintiff Name',
  'Law Firm',
  'Attorney',
  'Funding Company',
  'Medical Facility',
  'Case Manager',
  'Medical Provider',
  'Lien Status',
  'Total Liens',
  'Open Liens',
  'Closed Liens',
  'Total Cases',
  'Open Cases',
  'Closed Cases',
];

const STEPS = ['Details', 'Filters', 'Columns'];

export default function CreateUpdateReport({ onClose }: any) {
  const [currentStep, setCurrentStep] = useState(0);
  const [selectedCols, setSelectedCols] = useState<string[]>([]);
  const [available, setAvailable] = useState(AVAILABLE_COLUMNS);
  const [leftSearch, setLeftSearch] = useState('');
  const [rightSearch, setRightSearch] = useState('');

  const moveToSelected = (col: string) => {
    setAvailable((a) => a.filter((c) => c !== col));
    setSelectedCols((s) => [...s, col]);
  };

  const moveToAvailable = (col: string) => {
    setSelectedCols((s) => s.filter((c) => c !== col));
    setAvailable((a) => [...a, col]);
  };

  const selectAll = () => {
    setSelectedCols([...AVAILABLE_COLUMNS]);
    setAvailable([]);
  };

  const resetAll = () => {
    setSelectedCols([]);
    setAvailable([...AVAILABLE_COLUMNS]);
  };

  const filteredAvailable = available.filter((c) =>
    c.toLowerCase().includes(leftSearch.toLowerCase())
  );

  const filteredSelected = selectedCols.filter((c) =>
    c.toLowerCase().includes(rightSearch.toLowerCase())
  );

  return (
    <div className="fixed inset-0 bg-black/30 overflow-y-auto">
      <div className="min-h-full flex items-center justify-center p-4">
        <div className="bg-white w-full max-w-3xl rounded-xl p-6 space-y-6 my-8">

          {/* STEP HEADER */}
          <div className="flex justify-between">
            <h2 className="font-semibold text-lg">Create Report</h2>
            <button onClick={onClose} className="text-gray-500">✕</button>
          </div>

          <div className="relative mb-8 px-4">

            {/* Background line */}
            <div className="absolute top-4 left-4 right-4 h-px bg-gray-200" />

            {/* Active progress line */}
            <div
              className="absolute top-4 left-4 h-px bg-primary transition-all duration-300"
              style={{
                width:
                  currentStep === 0
                    ? '0%'
                    : `calc(${(currentStep / (STEPS.length - 1)) * 100}% - 2rem)`,
              }}
            />

            {/* Steps */}
            <div className="relative flex justify-between">
              {STEPS.map((step, i) => (
                <div
                  key={step}
                  className="flex flex-col items-center bg-white px-2"
                >
                  {/* Circle */}
                  <div
                    className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium transition-all duration-300 ${
                      i <= currentStep
                        ? 'bg-primary text-white'
                        : 'bg-gray-100 text-gray-400 border border-gray-200'
                    }`}
                  >
                    {i < currentStep ? (
                      <i className="ri-check-line" />
                    ) : (
                      i + 1
                    )}
                  </div>

                  {/* Label */}
                  <span
                    className={`mt-2 text-xs font-medium transition-colors ${
                      i <= currentStep
                        ? 'text-gray-900'
                        : 'text-gray-400'
                    }`}
                  >
                    {step}
                  </span>
                </div>
              ))}
            </div>
          </div>

          {/* STEP 1 */}
          {currentStep === 0 && (
            <div className="bg-white border border-gray-200 rounded-lg px-5 py-5">
              {/* <h2 className="text-sm font-semibold text-gray-900 mb-4">Report Details</h2> */}
              <div className="grid grid-cols-1 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Report Name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    placeholder="e.g. CV-2025-00123"
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Description
                  </label>
                  <textarea
                    rows={3}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
                  />
                </div>
              </div>
            </div>
          )}

          {/* STEP 2 */}
          {currentStep === 1 && (
            <div className="bg-white border border-gray-200 rounded-lg px-5 py-5">
              {/* <h2 className="text-sm font-semibold text-gray-900 mb-4">Report Filters</h2> */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    View By <span className="text-red-500">*</span>
                  </label>
                  <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
                    <option value="">Select…</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Status <span className="text-red-500">*</span>
                  </label>
                  <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
                    <option value="">Select…</option>
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Closed Date</label>
                  <input
                    type="date"
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Purchase Date</label>
                  <input
                    type="date"
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Law Firm
                  </label>
                  <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
                    <option value="">Select…</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Plaintiff Name
                  </label>
                  <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
                    <option value="">Select…</option>
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Attorney
                  </label>
                  <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
                    <option value="">Select…</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Funding Company
                  </label>
                  <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
                    <option value="">Select…</option>
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Medical Facility
                  </label>
                  <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
                    <option value="">Select…</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Case Manager
                  </label>
                  <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
                    <option value="">Select…</option>
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Medical Provider
                  </label>
                  <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
                    <option value="">Select…</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Lien Status
                  </label>
                  <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
                    <option value="">Select…</option>
                  </select>
                </div>

                <div className="sm:col-span-2">
                  <label className="flex items-center gap-3 cursor-pointer">
                    <input
                      type="checkbox"
                      className="w-4 h-4 rounded border-gray-300 text-primary focus:ring-primary"
                    />
                    <div>
                      <p className="text-sm font-medium text-gray-700">BULK</p>
                      <p className="text-xs text-gray-400">
                        Mark as Bulk.
                      </p>
                    </div>
                  </label>
                </div>

              </div>

            </div>
          )}

          {/* STEP 3 */}
          {currentStep === 2 && (
            <div className="grid grid-cols-2 gap-4">

              {/* LEFT */}
              <div className="border border-gray-200 rounded p-3">
                <div className="flex justify-between text-sm mb-2">
                  <span>Available Columns</span>
                  <button className="text-xs text-primary" onClick={selectAll} >Select All</button>
                </div>
                <input
                  value={leftSearch}
                  onChange={(e) => setLeftSearch(e.target.value)}
                  placeholder="Search..."
                  className="w-full mb-2 border border-gray-300 rounded px-2 py-1 text-sm"
                />
                <div className="space-y-2 max-h-64 overflow-auto">
                  {filteredAvailable.map((c) => (
                    <div 
                      onClick={() => moveToSelected(c)}
                      key={c}
                      className="flex justify-between border border-gray-200 p-2 rounded text-sm hover:bg-gray-200"
                    >
                      {c}
                      <button>→</button>
                    </div>
                  ))}
                </div>
              </div>

              {/* RIGHT */}
              <div className="border border-gray-200 rounded p-3">
                <div className="flex justify-between text-sm mb-2">
                  <span>Selected Columns</span>
                  <button onClick={resetAll} className="text-xs text-red-500">
                    Reset
                  </button>
                </div>
                <input
                  value={rightSearch}
                  onChange={(e) => setRightSearch(e.target.value)}
                  placeholder="Search..."
                  className="w-full mb-2 border border-gray-300 rounded px-2 py-1 text-sm"
                />
                <div className="space-y-2 max-h-64 overflow-auto">
                  {filteredSelected.map((c) => (
                    <div
                      onClick={() => moveToAvailable(c)}
                      key={c}
                      className="flex justify-between border border-gray-200 p-2 rounded text-sm hover:bg-gray-200"
                    >
                      {c}
                      <button>←</button>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* FOOTER */}
          <div className="flex justify-between">
            <button
              onClick={() => setCurrentStep((s) => Math.max(0, s - 1))}
              disabled={currentStep === 0}
              className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-40 disabled:cursor-not-allowed"
            >
              Back
            </button>

            {currentStep < STEPS.length - 1 ? (
              <button
                onClick={() => setCurrentStep((s) => s + 1)}
                className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Next
              </button>
            ) : (
              <button className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed">
                Generate
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
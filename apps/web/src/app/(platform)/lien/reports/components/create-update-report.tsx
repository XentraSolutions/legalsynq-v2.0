'use client';

import { useState } from 'react';
import { Modal } from '@/components/lien/modal';

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

  const isLastStep = currentStep === STEPS.length - 1;

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

  const handleBackOrCancel = () => {
    if (currentStep > 0) {
      setCurrentStep((s) => s - 1);
    } else {
      onClose();
    }
  };

  const handleNextOrSubmit = () => {
    if (!isLastStep) {
      setCurrentStep((s) => s + 1);
      return;
    }

    console.log('Generate report', { selectedCols });
    onClose();
  };

  const filteredAvailable = available.filter((c) =>
    c.toLowerCase().includes(leftSearch.toLowerCase())
  );

  const filteredSelected = selectedCols.filter((c) =>
    c.toLowerCase().includes(rightSearch.toLowerCase())
  );

  return (
    <Modal
      open={true}
      onClose={onClose}
      title="Create Report"
      subtitle="Configure your report step by step"
      size="lg"
      footer={
        <div className="flex justify-between w-full">
          {/* LEFT BUTTON */}
          <button
            onClick={handleBackOrCancel}
            className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
          >
            {currentStep === 0 ? 'Cancel' : 'Back'}
          </button>

          {/* RIGHT BUTTON */}
          <button
            onClick={handleNextOrSubmit}
            className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90"
          >
            {isLastStep ? 'Generate' : 'Next'}
          </button>
        </div>
      }
    >
      {/* STEP PROGRESS */}
      <div className="relative mb-8 px-4">
        <div className="absolute top-4 left-4 right-4 h-px bg-gray-200" />

        <div
          className="absolute top-4 left-4 h-px bg-primary transition-all duration-300"
          style={{
            width:
              currentStep === 0
                ? '0%'
                : `calc(${(currentStep / (STEPS.length - 1)) * 100}% - 2rem)`,
          }}
        />

        <div className="relative flex justify-between">
          {STEPS.map((step, i) => (
            <div key={step} className="flex flex-col items-center bg-white px-2">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
                  i <= currentStep
                    ? 'bg-primary text-white'
                    : 'bg-gray-100 text-gray-400 border border-gray-200'
                }`}
              >
                {i < currentStep ? <i className="ri-check-line" /> : i + 1}
              </div>

              <span
                className={`mt-2 text-xs font-medium ${
                  i <= currentStep ? 'text-gray-900' : 'text-gray-400'
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
          <div className="grid grid-cols-1 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Report Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                placeholder="Enter Report Name"
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Description
              </label>
              <textarea
                rows={3}
                placeholder='Enter Report Description'
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
              />
            </div>
          </div>
        </div>
      )}

      {/* STEP 2 */}
      {currentStep === 1 && (
        <div className="bg-white border border-gray-200 rounded-lg px-5 py-5">
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


    </Modal>
  );
}
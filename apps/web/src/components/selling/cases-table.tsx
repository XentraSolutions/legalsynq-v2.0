"use client";

import { useMemo } from "react";
import { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "../ui/base-table";
import { StatusBadge } from "@/components/lien/status-badge";
import { DateDisplay } from "@/components/ui/date-display";
import type { CaseListItem } from "@/lib/cases";
import {
  TABLE_CELL_CLASSNAME,
  TABLE_HEADER_CLASSNAME,
  TABLE_HEADER_CELL_CLASSNAME,
} from "@/components/selling/table-cell-styles";

export function CasesTable() {
  const columns = useMemo<ColumnDef<CaseListItem, any>[]>(
    () => [
      {
        id: "caseNumber",
        header: "Case ID",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.caseNumber}
          </span>
        ),
      },
      {
        id: "clientName",
        header: "Plaintiff Name",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.clientName}
          </span>
        ),
      },
      {
        id: "lawFirm",
        header: "Law Firm",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>{row.original.lawFirm}</span>
        ),
      },
      {
        id: "caseManager",
        header: "Case Manager",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.caseManager}
          </span>
        ),
      },
      {
        id: "accidentType",
        header: "Accident Type",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.accidentType}
          </span>
        ),
      },
      {
        id: "dateOfIncident",
        header: "Date of Loss",
        cell: ({ row }) => (
          <DateDisplay value={row.original.dateOfIncident} format="date" />
        ),
      },
      {
        id: "clientDob",
        header: "Date of Birth",
        cell: ({ row }) => (
          <DateDisplay value={row.original.clientDob} format="date" />
        ),
      },
      {
        id: "status",
        header: "Status",
        cell: ({ row }) => (
          <StatusBadge
            status={row.original.status}
            label={row.original.statusLabel}
          />
        ),
      },
    ],
    [],
  );

  return (
    <div className="bg-white overflow-hidden">
      <BaseTable
        data={[]}
        columns={columns}
        getRowId={(c) => c.id}
        isLoading={false}
        emptyMessage="No cases yet."
        className="bg-white border-0 rounded-none"
        headerClassName={TABLE_HEADER_CLASSNAME}
        headerCellClassName={TABLE_HEADER_CELL_CLASSNAME}
      />
    </div>
  );
}

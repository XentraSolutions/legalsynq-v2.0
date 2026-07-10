"use client";

import { useState, useEffect, useCallback } from "react";
import { useLienStore } from "@/stores/lien-store";
import { contactsApi } from "@/lib/contacts/contacts.api";
import { type ContactResponseDto } from "@/lib/contacts/contacts.types";
import { ApiError } from "@/lib/api-client";
import { ConfirmDialog } from "@/components/lien/modal";
import { AddContactModal } from "@/components/lien/add-contact-modal";
import { ActionMenu } from "@/components/lien/action-menu";

interface Props {
  facilityId: string;
}

const CONTACT_TYPE = "MedicalFacility";
const CONTACT_SUBTYPE = "FacilityContactPerson";
const PAGE_SIZE = 12;

export function MedicalFacilityStaffSection({ facilityId }: Props) {
  const addToast = useLienStore((s) => s.addToast);
  const [staff, setStaff] = useState<ContactResponseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [viewMode, setViewMode] = useState<"tile" | "list">("tile");
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ContactResponseDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ContactResponseDto | null>(null);
  const [deleting, setDeleting] = useState(false);

  const fetchStaff = useCallback(async () => {
    try {
      setLoading(true);
      const { data } = await contactsApi.list({
        FacilityId: facilityId,
        ContactType: CONTACT_TYPE,
        ContactSubtype: CONTACT_SUBTYPE,
      });
      setStaff(Array.isArray(data.items) ? data.items : []);
    } catch {
      setStaff([]);
    } finally {
      setLoading(false);
    }
  }, [facilityId]);

  useEffect(() => {
    fetchStaff();
  }, [fetchStaff]);

  const openAdd = () => {
    setEditTarget(null);
    setModalOpen(true);
  };

  const openEdit = (s: ContactResponseDto) => {
    setEditTarget(s);
    setModalOpen(true);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await contactsApi.delete(deleteTarget.id);
      addToast({
        type: "success",
        title: "Staff Removed",
        description: `${deleteTarget.firstName} ${deleteTarget.lastName} has been removed.`,
      });
      setDeleteTarget(null);
      fetchStaff();
    } catch (err) {
      addToast({
        type: "error",
        title: "Delete Failed",
        description:
          err instanceof ApiError
            ? err.message
            : "An unexpected error occurred",
      });
      setDeleteTarget(null);
    } finally {
      setDeleting(false);
    }
  };

  const totalPages = Math.ceil(staff.length / PAGE_SIZE);
  const paged = staff.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <i className="ri-team-line text-gray-500" />
          <h3 className="text-sm font-semibold text-gray-800">
            Medical Facility Staff
          </h3>
          {!loading && (
            <span className="text-xs text-gray-400 bg-gray-100 rounded-full px-2 py-0.5">
              {staff.length}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setViewMode("tile")}
            title="Tile view"
            className={`p-1.5 rounded-lg transition-colors ${viewMode === "tile" ? "bg-primary/10 text-primary" : "text-gray-400 hover:bg-gray-100"}`}
          >
            <i className="ri-layout-grid-line text-base" />
          </button>
          <button
            onClick={() => setViewMode("list")}
            title="List view"
            className={`p-1.5 rounded-lg transition-colors ${viewMode === "list" ? "bg-primary/10 text-primary" : "text-gray-400 hover:bg-gray-100"}`}
          >
            <i className="ri-list-unordered text-base" />
          </button>
          <button
            onClick={openAdd}
            className="flex items-center gap-1.5 text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90"
          >
            <i className="ri-add-line" />
            Add Staff
          </button>
        </div>
      </div>

      <div className="p-5">
        {loading ? (
          <div className="text-center py-10 text-sm text-gray-400">
            Loading staff...
          </div>
        ) : staff.length === 0 ? (
          <div className="text-center py-10 text-sm text-gray-400">
            No staff members yet. Add the first one.
          </div>
        ) : viewMode === "tile" ? (
          <TileView staff={paged} onEdit={openEdit} onDelete={setDeleteTarget} />
        ) : (
          <ListView staff={paged} onEdit={openEdit} onDelete={setDeleteTarget} />
        )}

        {totalPages > 1 && (
          <div className="flex items-center justify-center gap-2 mt-4 pt-4 border-t border-gray-100">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40"
            >
              Previous
            </button>
            <span className="text-sm text-gray-500">
              Page {page} of {totalPages}
            </span>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
              className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40"
            >
              Next
            </button>
          </div>
        )}
      </div>

      {modalOpen && (
        <AddContactModal
          open={modalOpen}
          onClose={() => setModalOpen(false)}
          title={editTarget ? "Edit Staff Member" : "Add Staff Member"}
          subtitle="Medical Facility Staff"
          contactType={CONTACT_TYPE}
          contactSubtype={CONTACT_SUBTYPE}
          facilityId={facilityId}
          editTarget={editTarget}
          onSaved={() => {
            setModalOpen(false);
            fetchStaff();
          }}
        />
      )}

      {deleteTarget && (
        <ConfirmDialog
          open
          onClose={() => setDeleteTarget(null)}
          onConfirm={handleDelete}
          title="Delete Staff Member"
          description={
            <>
              Are you sure you want to delete{" "}
              <span className="font-semibold text-primary">
                {deleteTarget.firstName} {deleteTarget.lastName}
              </span>
              ? This action cannot be undone and will permanently remove all
              associated data.
            </>
          }
          confirmLabel="Delete"
          confirmVariant="danger"
          loading={deleting}
          warningTitle="Warning: Deleting this staff member will also remove:"
          warningItems={[
            "All case associations",
            "All uploaded documents",
            "All activity history",
          ]}
        />
      )}
    </div>
  );
}

function TileView({
  staff,
  onEdit,
  onDelete,
}: {
  staff: ContactResponseDto[];
  onEdit: (s: ContactResponseDto) => void;
  onDelete: (s: ContactResponseDto) => void;
}) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      {staff.map((s) => (
        <div
          key={s.id}
          className="border border-gray-200 rounded-xl p-4 hover:shadow-sm transition-shadow"
        >
          <div className="flex items-start justify-between mb-3">
            <p className="text-sm font-semibold text-gray-900 leading-snug">
              {s.firstName} {s.lastName}
            </p>
            <ActionMenu
              items={[
                {
                  label: "Edit",
                  icon: "ri-edit-line",
                  onClick: () => onEdit(s),
                },
                {
                  label: "Delete",
                  icon: "ri-delete-bin-line",
                  onClick: () => onDelete(s),
                  variant: "danger",
                  divider: true,
                },
              ]}
            />
          </div>
          {s.organization && (
            <p className="text-xs text-gray-500 mb-1.5">{s.organization}</p>
          )}
          <div className="flex items-center gap-2 text-xs text-gray-500 mb-1.5">
            <i className="ri-mail-line text-gray-400 shrink-0" />
            <span className="truncate">{s.email || "--"}</span>
          </div>
          <div className="flex items-center gap-2 text-xs text-gray-500">
            <i className="ri-phone-line text-gray-400 shrink-0" />
            <span>{s.phone || "--"}</span>
          </div>
        </div>
      ))}
    </div>
  );
}

function ListView({
  staff,
  onEdit,
  onDelete,
}: {
  staff: ContactResponseDto[];
  onEdit: (s: ContactResponseDto) => void;
  onDelete: (s: ContactResponseDto) => void;
}) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-gray-100">
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">
              Name
            </th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">
              Organization
            </th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">
              Email
            </th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">
              Phone
            </th>
            <th className="px-3 py-2.5" />
          </tr>
        </thead>
        <tbody>
          {staff.map((s) => (
            <tr
              key={s.id}
              className="border-b border-gray-50 hover:bg-gray-50/50"
            >
              <td className="px-3 py-3 text-gray-900 font-medium">
                {s.firstName} {s.lastName}
              </td>
              <td className="px-3 py-3 text-gray-500">
                {s.organization || "—"}
              </td>
              <td className="px-3 py-3 text-gray-500">{s.email || "—"}</td>
              <td className="px-3 py-3 text-gray-500">{s.phone || "—"}</td>
              <td className="px-3 py-3">
                <ActionMenu
                  items={[
                    {
                      label: "Edit",
                      icon: "ri-edit-line",
                      onClick: () => onEdit(s),
                    },
                    {
                      label: "Delete",
                      icon: "ri-delete-bin-line",
                      onClick: () => onDelete(s),
                      variant: "danger",
                      divider: true,
                    },
                  ]}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

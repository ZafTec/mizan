import { clientApi } from "@/lib/api.client";
import type { components, paths } from "@/types/api.generated";

/**
 * Browser calls for the admin mail log, announcements, the contact inbox and
 * support notes, plus the public contact form. Every type comes from the
 * generated OpenAPI document. Writes a person may retry carry an
 * Idempotency-Key (docs/ARCHITECTURE.md#native-clients).
 */
type Schemas = components["schemas"];

export type EmailMessage = Schemas["EmailMessageDto"];
export type EmailMessagePage = Schemas["EmailMessageDtoPagedResult"];
export type MailStats = Schemas["MailStatsDto"];
export type AnnouncementAudience = Schemas["AnnouncementAudienceDto"];
export type AnnouncementResult = Schemas["AnnouncementResultDto"];
export type SendAnnouncementRequest = Schemas["SendAnnouncementRequest"];
export type ContactRequest = Schemas["ContactRequest"];
export type ContactAccepted = Schemas["ContactAcceptedDto"];
export type ContactSubmission = Schemas["ContactSubmissionDto"];
export type ContactSubmissionPage = Schemas["ContactSubmissionDtoPagedResult"];
export type SupportNote = Schemas["SupportNoteDto"];
export type SystemHealth = Schemas["SystemHealthDto"];
export type SystemHealthCheck = Schemas["SystemHealthCheckDto"];
export type AdminHouseholdDetail = Schemas["AdminHouseholdDetailDto"];
export type AdminHouseholdSummaryPage =
  Schemas["AdminHouseholdSummaryPagedResult"];
export type AdminUserPage = Schemas["AdminUserDtoPagedResult"];
export type MailQuery = NonNullable<
  paths["/api/admin/mail"]["get"]["parameters"]["query"]
>;
export type ContactQuery = NonNullable<
  paths["/api/admin/contact"]["get"]["parameters"]["query"]
>;

export const MAIL_STATUSES = ["Queued", "Sent", "Failed"] as const;
export const CONTACT_STATUSES = ["New", "Handled"] as const;
export type SupportNoteSubject = "User" | "Household";

function idempotent(): Record<string, string> {
  return { "Idempotency-Key": crypto.randomUUID() };
}

export const retryEmail = (id: string) =>
  clientApi<EmailMessage>(`/api/admin/mail/${id}/retry`, {
    method: "POST",
    headers: idempotent(),
  });

export const previewAnnouncementAudience = (audience: string) =>
  clientApi<AnnouncementAudience>(
    `/api/admin/mail/announcements/audience?audience=${encodeURIComponent(audience)}`,
  );

/**
 * The announcement id is made once per confirmation, so pressing Send twice
 * or retrying after a lost answer queues nothing new.
 */
export const sendAnnouncement = (body: SendAnnouncementRequest) =>
  clientApi<AnnouncementResult>("/api/admin/mail/announcements", {
    method: "POST",
    body,
    headers: { "Idempotency-Key": body.announcementId ?? crypto.randomUUID() },
  });

export const searchHouseholds = (search: string) =>
  clientApi<AdminHouseholdSummaryPage>(
    `/api/Households/admin/all?search=${encodeURIComponent(search)}&pageSize=8`,
  );

export const searchUsers = (search: string) =>
  clientApi<AdminUserPage>(
    `/api/admin/users?search=${encodeURIComponent(search)}&pageSize=8`,
  );

/** Public. The hidden field is sent only when something filled it in. */
export const submitContact = (body: ContactRequest) =>
  clientApi<ContactAccepted>("/api/Contact", {
    method: "POST",
    body,
    requireAuth: false,
    headers: idempotent(),
    expectedStatuses: [400, 429],
  });

export const markContactHandled = (id: string) =>
  clientApi<ContactSubmission>(`/api/admin/contact/${id}/handled`, {
    method: "POST",
    headers: idempotent(),
  });

export const replyToContact = (id: string, body: string) =>
  clientApi<ContactSubmission>(`/api/admin/contact/${id}/reply`, {
    method: "POST",
    body: { body },
    headers: idempotent(),
  });

export const listSupportNotes = (subjectType: SupportNoteSubject, id: string) =>
  clientApi<SupportNote[]>(
    `/api/admin/support-notes?subjectType=${subjectType}&subjectId=${id}`,
  );

export const addSupportNote = (
  subjectType: SupportNoteSubject,
  subjectId: string,
  body: string,
) =>
  clientApi<SupportNote>("/api/admin/support-notes", {
    method: "POST",
    body: { subjectType, subjectId, body },
    headers: idempotent(),
  });

export const updateSupportNote = (id: string, body: string) =>
  clientApi<SupportNote>(`/api/admin/support-notes/${id}`, {
    method: "PUT",
    body: { body },
    headers: idempotent(),
  });

export const deleteSupportNote = (id: string) =>
  clientApi<void>(`/api/admin/support-notes/${id}`, { method: "DELETE" });

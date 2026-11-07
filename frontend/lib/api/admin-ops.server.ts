import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import type {
  AdminHouseholdDetail,
  ContactQuery,
  ContactSubmission,
  ContactSubmissionPage,
  EmailMessage,
  EmailMessagePage,
  MailQuery,
  MailStats,
  SupportNote,
  SupportNoteSubject,
  SystemHealth,
} from "./admin-ops";

/**
 * Server-component reads for the admin mail log, the contact inbox, system
 * health and the household detail. They forward the session cookie and
 * answer with the generated types.
 */
const log = logger.createModuleLogger("admin-ops");

function query(values: Record<string, string | number | null | undefined>) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values))
    if (value !== undefined && value !== null && value !== "")
      params.set(key, String(value));
  return params.toString();
}

export const listMailServer = (filters: MailQuery) =>
  serverApi<EmailMessagePage>(`/api/admin/mail?${query(filters)}`);

export const getMailStatsServer = () =>
  serverApi<MailStats>("/api/admin/mail/stats");

/** Null for an id that is not in the log, so the drawer simply stays shut. */
export async function getMailServer(id: string): Promise<EmailMessage | null> {
  try {
    return await serverApi<EmailMessage>(`/api/admin/mail/${id}`, {
      expectedStatuses: [400, 404],
    });
  } catch {
    return null;
  }
}

export const listContactServer = (filters: ContactQuery) =>
  serverApi<ContactSubmissionPage>(`/api/admin/contact?${query(filters)}`);

export async function getContactServer(
  id: string,
): Promise<ContactSubmission | null> {
  try {
    return await serverApi<ContactSubmission>(`/api/admin/contact/${id}`, {
      expectedStatuses: [400, 404],
    });
  } catch {
    return null;
  }
}

/** Null when the health read fails, so the overview still shows its counts. */
export async function getSystemHealthServer(): Promise<SystemHealth | null> {
  try {
    return await serverApi<SystemHealth>("/api/admin/overview/health");
  } catch (error) {
    log.error("Failed to load system health", {
      error: error instanceof Error ? error.message : String(error),
    });
    return null;
  }
}

export async function getAdminHouseholdServer(
  id: string,
): Promise<AdminHouseholdDetail | null> {
  try {
    return await serverApi<AdminHouseholdDetail>(
      `/api/Households/admin/${id}`,
      { expectedStatuses: [404] },
    );
  } catch {
    return null;
  }
}

/** Empty when the read fails: a missing notes panel must not hide the rest of the page. */
export async function listSupportNotesServer(
  subjectType: SupportNoteSubject,
  subjectId: string,
): Promise<SupportNote[]> {
  try {
    return await serverApi<SupportNote[]>(
      `/api/admin/support-notes?subjectType=${subjectType}&subjectId=${subjectId}`,
    );
  } catch (error) {
    log.error("Failed to load support notes", {
      error: error instanceof Error ? error.message : String(error),
    });
    return [];
  }
}

import { notFound } from "next/navigation";

/** Any other /tg path renders app/tg/not-found.tsx inside the Mini App frame. */
export default function MiniAppMissingPage() {
  notFound();
}

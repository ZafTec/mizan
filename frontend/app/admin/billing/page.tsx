import { getAdminBillingCatalog } from "@/data/subscription";
import BillingAdmin from "./BillingAdmin";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Billing | Mizan admin",
  description: "The Pro plans and deals on sale, created in Paddle",
};

export default async function AdminBillingPage() {
  const catalog = await getAdminBillingCatalog();

  return (
    <>
      <AdminPageHeader
        title="Billing"
        lead="What Pro costs; every change is made in Paddle first, then shows on the pricing page and at checkout."
      />
      {catalog ? (
        <BillingAdmin catalog={catalog} />
      ) : (
        <p role="alert" className="text-[14px] text-destructive">
          The billing catalogue could not be loaded.
        </p>
      )}
    </>
  );
}

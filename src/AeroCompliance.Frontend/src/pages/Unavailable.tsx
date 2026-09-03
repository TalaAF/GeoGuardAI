import { Ban, FileQuestion, ShieldAlert } from "lucide-react";
import { Link } from "react-router-dom";
import { Card, Empty, PageTitle } from "../components/Common";
export default function Unavailable({
  feature,
  access,
  notFound,
}: {
  feature?: string;
  access?: boolean;
  notFound?: boolean;
}) {
  const title = access
    ? "Access denied"
    : notFound
      ? "Page not found"
      : `${feature || "Feature"} unavailable`;
  const detail = access
    ? "Your authenticated role does not allow this page. The backend remains authoritative."
    : notFound
      ? "The requested frontend route does not exist."
      : "No backend endpoint currently provides this feature. No placeholder operational records are shown.";
  const Icon = access ? Ban : notFound ? FileQuestion : ShieldAlert;
  return (
    <div className="page">
      <PageTitle title={title} />
      <Card>
        <div className="empty">
          <Icon size={34} color="#94a3b8" />
          <Empty title={title} detail={detail} />
          <Link className="primary" to="/dashboard">
            Return to dashboard
          </Link>
        </div>
      </Card>
    </div>
  );
}

import { useAuth } from "../auth/AuthContext";
import { Card, PageTitle, ShortId } from "../components/Common";
export default function SettingsPage() {
  const { user } = useAuth();
  return (
    <div className="page">
      <PageTitle
        title="Settings"
        description="Current authenticated profile and environment-safe display preferences."
      />
      <Card>
        <h2>Current profile</h2>
        <div className="detail-grid">
          <div className="detail">
            <small>User</small>
            <strong>{user?.fullName}</strong>
          </div>
          <div className="detail">
            <small>Email</small>
            <strong>{user?.email}</strong>
          </div>
          <div className="detail">
            <small>Role</small>
            <strong>{user?.role.name}</strong>
          </div>
          <div className="detail">
            <small>Municipality</small>
            <strong>{user?.municipality?.name || "Platform-wide"}</strong>
          </div>
          <div className="detail">
            <small>User ID</small>
            <ShortId value={user?.id} />
          </div>
          <div className="detail">
            <small>Session storage</small>
            <strong>Current browser tab only</strong>
          </div>
        </div>
      </Card>
    </div>
  );
}

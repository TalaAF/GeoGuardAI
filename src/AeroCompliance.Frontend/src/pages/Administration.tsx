import { Plus, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { api, errorMessage, page as getPage } from "../api/client";
import type { Municipality, Role, UserInfo } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import {
  Card,
  Empty,
  Notice,
  PageTitle,
  ShortId,
  Spinner,
  Status,
  fmtDate,
} from "../components/Common";

const roles: Role[] = [
  "Admin",
  "MunicipalityManager",
  "Inspector",
  "GISAnalyst",
  "Reviewer",
  "DroneOperator",
];
export default function Administration() {
  const { user } = useAuth();
  const [tab, setTab] = useState<"users" | "municipalities">("users");
  const [users, setUsers] = useState<UserInfo[] | null>(null);
  const [municipalities, setMunicipalities] = useState<Municipality[]>([]);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [show, setShow] = useState(false);
  const [editing, setEditing] = useState<UserInfo | null>(null);
  const [editingMunicipality, setEditingMunicipality] =
    useState<Municipality | null>(null);
  const [userForm, setUserForm] = useState({
    fullName: "",
    email: "",
    initialPassword: "",
    role: "Inspector" as Role,
    municipalityId: user?.municipality?.id || "",
  });
  const [municipalityForm, setMunicipalityForm] = useState({
    name: "",
    region: "",
    apiAccessEnabled: true,
    isPilot: false,
  });
  const load = useCallback(async () => {
    setError("");
    try {
      setUsers((await getPage<UserInfo>("/users", { pageSize: 100 })).items);
      if (user?.role.name === "Admin")
        setMunicipalities(
          (await getPage<Municipality>("/municipalities", { pageSize: 100 }))
            .items,
        );
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [user]);
  useEffect(() => {
    void load();
  }, [load]);
  const resetUser = () => {
    setEditing(null);
    setUserForm({
      fullName: "",
      email: "",
      initialPassword: "",
      role: "Inspector",
      municipalityId: user?.municipality?.id || "",
    });
  };
  const saveUser = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      if (editing)
        await api.put(`/users/${editing.id}`, {
          fullName: userForm.fullName,
          email: userForm.email,
          role: userForm.role,
          municipalityId:
            userForm.role === "Admin"
              ? null
              : userForm.municipalityId || undefined,
        });
      else
        await api.post("/users", {
          ...userForm,
          municipalityId:
            userForm.role === "Admin"
              ? null
              : userForm.municipalityId || undefined,
        });
      setUserForm((x) => ({ ...x, initialPassword: "" }));
      setShow(false);
      resetUser();
      setMessage(
        editing
          ? "User updated."
          : "User created; the initial password was cleared from the form.",
      );
      await load();
    } catch (e) {
      setError(errorMessage(e));
    }
  };
  const editUser = (x: UserInfo) => {
    setEditing(x);
    setUserForm({
      fullName: x.fullName,
      email: x.email,
      initialPassword: "",
      role: x.role.name,
      municipalityId: x.municipality?.id || "",
    });
    setShow(true);
  };
  const toggle = async (x: UserInfo) => {
    try {
      await api.post(
        `/users/${x.id}/${x.isActive ? "deactivate" : "activate"}`,
        { reason: "Changed from administration screen." },
      );
      await load();
    } catch (e) {
      setError(errorMessage(e));
    }
  };
  const saveMunicipality = async (e: FormEvent) => {
    e.preventDefault();
    try {
      if (editingMunicipality)
        await api.put(
          `/municipalities/${editingMunicipality.id}`,
          municipalityForm,
        );
      else await api.post("/municipalities", municipalityForm);
      setMunicipalityForm({
        name: "",
        region: "",
        apiAccessEnabled: true,
        isPilot: false,
      });
      setShow(false);
      setMessage(
        editingMunicipality ? "Municipality updated." : "Municipality created.",
      );
      setEditingMunicipality(null);
      await load();
    } catch (e) {
      setError(errorMessage(e));
    }
  };
  return (
    <div className="page">
      <PageTitle
        title="Administration"
        description="Manage records within backend-enforced role and tenant boundaries."
        actions={
          <button className="secondary" onClick={() => void load()}>
            <RefreshCw size={14} />
            Refresh
          </button>
        }
      />
      {error && <Notice kind="error">{error}</Notice>}
      {message && <Notice kind="success">{message}</Notice>}
      <Card>
        <div className="tabs">
          <button
            className={tab === "users" ? "active" : ""}
            onClick={() => {
              setTab("users");
              setShow(false);
            }}
          >
            Users
          </button>
          {user?.role.name === "Admin" && (
            <button
              className={tab === "municipalities" ? "active" : ""}
              onClick={() => {
                setTab("municipalities");
                setShow(false);
              }}
            >
              Municipalities
            </button>
          )}
        </div>
        <button
          className="primary"
          onClick={() => {
            setShow(!show);
            resetUser();
            setEditingMunicipality(null);
            setMunicipalityForm({
              name: "",
              region: "",
              apiAccessEnabled: true,
              isPilot: false,
            });
          }}
        >
          <Plus size={14} />
          Create {tab === "users" ? "user" : "municipality"}
        </button>
        {show && tab === "users" && (
          <form className="form" onSubmit={saveUser} style={{ marginTop: 15 }}>
            <div className="field-row">
              <label>
                Full name
                <input
                  required
                  value={userForm.fullName}
                  onChange={(e) =>
                    setUserForm({ ...userForm, fullName: e.target.value })
                  }
                />
              </label>
              <label>
                Email
                <input
                  required
                  type="email"
                  value={userForm.email}
                  onChange={(e) =>
                    setUserForm({ ...userForm, email: e.target.value })
                  }
                />
              </label>
              <label>
                Role
                <select
                  value={userForm.role}
                  onChange={(e) =>
                    setUserForm({ ...userForm, role: e.target.value as Role })
                  }
                >
                  {roles
                    .filter((x) => user?.role.name === "Admin" || x !== "Admin")
                    .map((x) => (
                      <option key={x}>{x}</option>
                    ))}
                </select>
              </label>
              {user?.role.name === "Admin" && userForm.role !== "Admin" && (
                <label>
                  Municipality
                  <select
                    required
                    value={userForm.municipalityId}
                    onChange={(e) =>
                      setUserForm({
                        ...userForm,
                        municipalityId: e.target.value,
                      })
                    }
                  >
                    <option value="">Select…</option>
                    {municipalities.map((x) => (
                      <option key={x.id} value={x.id}>
                        {x.name}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              {!editing && (
                <label>
                  Initial password
                  <input
                    required
                    minLength={8}
                    type="password"
                    autoComplete="new-password"
                    value={userForm.initialPassword}
                    onChange={(e) =>
                      setUserForm({
                        ...userForm,
                        initialPassword: e.target.value,
                      })
                    }
                  />
                </label>
              )}
            </div>
            <div>
              <button className="primary">
                {editing ? "Save changes" : "Create user"}
              </button>
            </div>
          </form>
        )}
        {show && tab === "municipalities" && (
          <form
            className="form"
            onSubmit={saveMunicipality}
            style={{ marginTop: 15 }}
          >
            <div className="field-row">
              <label>
                Name
                <input
                  required
                  value={municipalityForm.name}
                  onChange={(e) =>
                    setMunicipalityForm({
                      ...municipalityForm,
                      name: e.target.value,
                    })
                  }
                />
              </label>
              <label>
                Region
                <input
                  value={municipalityForm.region}
                  onChange={(e) =>
                    setMunicipalityForm({
                      ...municipalityForm,
                      region: e.target.value,
                    })
                  }
                />
              </label>
            </div>
            <label>
              <input
                type="checkbox"
                checked={municipalityForm.apiAccessEnabled}
                onChange={(e) =>
                  setMunicipalityForm({
                    ...municipalityForm,
                    apiAccessEnabled: e.target.checked,
                  })
                }
              />{" "}
              API access enabled
            </label>
            <button className="primary">
              {editingMunicipality
                ? "Save municipality"
                : "Create municipality"}
            </button>
          </form>
        )}
        <div style={{ marginTop: 16 }}>
          {tab === "users" ? (
            !users ? (
              <Spinner />
            ) : users.length === 0 ? (
              <Empty
                title="No users"
                detail="No users are visible in your tenant scope."
              />
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Email</th>
                      <th>Role</th>
                      <th>Municipality</th>
                      <th>Status</th>
                      <th>Last login</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {users.map((x) => (
                      <tr key={x.id}>
                        <td>{x.fullName}</td>
                        <td>{x.email}</td>
                        <td>{x.role.name}</td>
                        <td>{x.municipality?.name || "Platform"}</td>
                        <td>
                          <Status
                            value={x.isActive ? "Completed" : "dismissed"}
                          />
                        </td>
                        <td>{fmtDate(x.lastLoginAt)}</td>
                        <td>
                          <div className="actions">
                            <button
                              className="secondary"
                              onClick={() => editUser(x)}
                            >
                              Edit
                            </button>
                            <button
                              className="secondary"
                              disabled={x.id === user?.id}
                              onClick={() => void toggle(x)}
                            >
                              {x.isActive ? "Deactivate" : "Activate"}
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )
          ) : municipalities.length === 0 ? (
            <Empty
              title="No municipalities"
              detail="No platform municipality records exist."
            />
          ) : (
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Region</th>
                    <th>Reference</th>
                    <th>API access</th>
                    <th>Created</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {municipalities.map((x) => (
                    <tr key={x.id}>
                      <td>{x.name}</td>
                      <td>{x.region || "—"}</td>
                      <td>
                        <ShortId value={x.id} />
                      </td>
                      <td>{x.apiAccessEnabled ? "Enabled" : "Disabled"}</td>
                      <td>{fmtDate(x.createdAt)}</td>
                      <td>
                        <button
                          className="secondary"
                          onClick={() => {
                            setEditingMunicipality(x);
                            setMunicipalityForm({
                              name: x.name,
                              region: x.region || "",
                              apiAccessEnabled: x.apiAccessEnabled,
                              isPilot: x.isPilot,
                            });
                            setShow(true);
                          }}
                        >
                          Edit
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </Card>
    </div>
  );
}

import { useEffect, useState, type FormEvent } from "react";
import { api, errorMessage } from "../api/client";
import type { FlightDetails } from "../api/types";
import { Notice } from "./Common";
import GeometryEditor from "./GeometryEditor";
import type { NormalizedMultiPolygon } from "../lib/geometry";

export default function FlightEdit({
  flight,
  onSaved,
}: {
  flight: FlightDetails;
  onSaved(): Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    missionName: "",
    scheduledAt: "",
    weatherConditions: "",
    qualityFlag: true,
    plannedArea: null as NormalizedMultiPolygon | null,
  });
  useEffect(
    () =>
      setForm({
        missionName: flight.missionName || "",
        scheduledAt: flight.scheduledAt
          ? new Date(flight.scheduledAt).toISOString().slice(0, 16)
          : "",
        weatherConditions: flight.weatherConditions || "",
        qualityFlag: flight.qualityFlag,
        plannedArea: null,
      }),
    [flight],
  );
  const save = async (event: FormEvent) => {
    event.preventDefault();
    setError("");
    try {
      await api.put(`/flights/${flight.id}`, {
        municipalityId: flight.municipalityId,
        droneOperatorId: flight.droneOperatorId,
        missionName: form.missionName || null,
        scheduledAt: form.scheduledAt
          ? new Date(form.scheduledAt).toISOString()
          : null,
        weatherConditions: form.weatherConditions || null,
        qualityFlag: form.qualityFlag,
        plannedArea: form.plannedArea,
      });
      setOpen(false);
      await onSaved();
    } catch (e) {
      setError(errorMessage(e));
    }
  };
  return (
    <section className="card">
      <div className="page-title" style={{ marginBottom: open ? 14 : 0 }}>
        <div>
          <h2 style={{ margin: 0 }}>Flight plan</h2>
          <p>Editable only while the backend status is planned.</p>
        </div>
        <button className="secondary" onClick={() => setOpen(!open)}>
          {open ? "Close" : "Edit flight"}
        </button>
      </div>
      {open && (
        <form className="form" onSubmit={save}>
          {error && <Notice kind="error">{error}</Notice>}
          <div className="field-row">
            <label>
              Mission name
              <input
                value={form.missionName}
                onChange={(e) =>
                  setForm({ ...form, missionName: e.target.value })
                }
              />
            </label>
            <label>
              Scheduled at
              <input
                type="datetime-local"
                value={form.scheduledAt}
                onChange={(e) =>
                  setForm({ ...form, scheduledAt: e.target.value })
                }
              />
            </label>
          </div>
          <label>
            Weather conditions
            <input
              value={form.weatherConditions}
              onChange={(e) =>
                setForm({ ...form, weatherConditions: e.target.value })
              }
            />
          </label>
          <label>
            Planned area (optional)
            <GeometryEditor
              initialGeometry={flight.plannedArea}
              onChange={(g) => setForm((f) => ({ ...f, plannedArea: g }))}
              boundaryLabel="planned area"
            />
          </label>
          <label>
            <input
              type="checkbox"
              checked={form.qualityFlag}
              onChange={(e) =>
                setForm({ ...form, qualityFlag: e.target.checked })
              }
            />{" "}
            Quality acceptable
          </label>
          <button className="primary">Save changes</button>
        </form>
      )}
    </section>
  );
}

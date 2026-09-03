import { useEffect, useState, type FormEvent } from "react";
import { api, errorMessage } from "../api/client";
import type { FlightDetails } from "../api/types";
import { Notice } from "./Common";

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
    plannedArea: "",
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
        plannedArea: flight.plannedArea
          ? JSON.stringify(flight.plannedArea, null, 2)
          : "",
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
        plannedArea: form.plannedArea.trim()
          ? JSON.parse(form.plannedArea)
          : null,
      });
      setOpen(false);
      await onSaved();
    } catch (e) {
      setError(
        e instanceof SyntaxError
          ? "Planned area must be valid GeoJSON."
          : errorMessage(e),
      );
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
            Planned area GeoJSON
            <textarea
              value={form.plannedArea}
              onChange={(e) =>
                setForm({ ...form, plannedArea: e.target.value })
              }
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

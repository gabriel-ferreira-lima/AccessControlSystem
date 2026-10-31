import type { ReactNode } from "react";
import type { ApiNotification } from "../../types/api";
import { IconAlertTriangle, IconCheckCircle, IconInfo } from "../icons";

type Tone = "error" | "success" | "info";

interface AlertProps {
  tone?: Tone;
  title: string;
  children?: ReactNode;
  notifications?: ApiNotification[];
}

const ICONS = {
  error: IconAlertTriangle,
  success: IconCheckCircle,
  info: IconInfo,
} as const;

export function Alert({ tone = "info", title, children, notifications }: AlertProps) {
  const Glyph = ICONS[tone];
  const hasList = notifications !== undefined && notifications.length > 0;

  return (
    <div className={`alert alert--${tone}`} role={tone === "error" ? "alert" : "status"}>
      <Glyph className="alert__icon" />
      <div className="alert__body">
        <p className="alert__title">{title}</p>
        {children ? <div className="alert__content">{children}</div> : null}
        {hasList ? (
          <ul className="alert__list">
            {notifications.map((item, index) => (
              <li key={`${item.key}-${index}`}>{item.message}</li>
            ))}
          </ul>
        ) : null}
      </div>
    </div>
  );
}

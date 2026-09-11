import { NavLink } from "react-router-dom";
import type { ComponentType, SVGProps } from "react";
import { useAuth } from "../../context/AuthContext";
import type { Role } from "../../types/api";
import { IconPanel, IconShield, IconUser, IconUsers } from "../icons";

interface NavItem {
  to: string;
  label: string;
  icon: ComponentType<SVGProps<SVGSVGElement>>;
  end: boolean;
  /** Perfil minimo exigido. Ausente = visivel para qualquer sessao. */
  requiresRole?: Role;
}

interface NavSection {
  label: string;
  items: NavItem[];
}

const SECTIONS: NavSection[] = [
  {
    label: "Operacao",
    items: [{ to: "/", label: "Inicio", icon: IconPanel, end: true }],
  },
  {
    label: "Conta",
    items: [
      {
        to: "/minha-conta",
        label: "Minha conta",
        icon: IconUser,
        end: true,
        // Administrador ja edita qualquer conta (inclusive a propria) em
        // Operadores > Editar; "Minha conta" fica exclusiva do Operador.
        requiresRole: "Operator",
      },
    ],
  },
  {
    label: "Operadores",
    items: [
      {
        to: "/operadores",
        label: "Operadores",
        icon: IconUsers,
        end: true,
        requiresRole: "Admin",
      },
    ],
  },
];

export function Sidebar() {
  const { session } = useAuth();
  const role = session?.role;

  // Filtro apenas visual: a API continua sendo a autoridade de acesso.
  const sections = SECTIONS.map((section) => ({
    ...section,
    items: section.items.filter((item) => !item.requiresRole || item.requiresRole === role),
  })).filter((section) => section.items.length > 0);

  return (
    <aside className="sidebar">
      <div className="sidebar__brand">
        <IconShield className="sidebar__brand-mark" />
        <span className="sidebar__brand-text">
          <span className="sidebar__brand-title">Controle de Acesso</span>
          <span className="sidebar__brand-subtitle">Console administrativo</span>
        </span>
      </div>

      <nav className="sidebar__nav" aria-label="Navegacao principal">
        {sections.map((section) => (
          <div className="sidebar__group" key={section.label}>
            <p className="sidebar__group-label">{section.label}</p>
            <ul className="sidebar__list">
              {section.items.map((item) => {
                const Glyph = item.icon;
                return (
                  <li key={item.to}>
                    <NavLink
                      to={item.to}
                      end={item.end}
                      className={({ isActive }) =>
                        isActive ? "sidebar__link sidebar__link--active" : "sidebar__link"
                      }
                    >
                      <Glyph className="sidebar__link-icon" />
                      <span>{item.label}</span>
                    </NavLink>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </nav>

      <p className="sidebar__footer">Acesso restrito a operadores autorizados.</p>
    </aside>
  );
}

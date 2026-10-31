import { useNavigate } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";
import { roleLabel } from "../../lib/labels";
import { Button } from "../ui/Button";
import { ThemeToggle } from "../ThemeToggle";
import { IconLogout } from "../icons";

export function Topbar() {
  const { session, signOut } = useAuth();
  const navigate = useNavigate();

  function handleSignOut() {
    signOut();
    navigate("/login", { replace: true });
  }

  return (
    <header className="topbar">
      <div className="topbar__identity">
        {session ? <span className="tag">{roleLabel(session.role)}</span> : null}
      </div>

      <div className="topbar__actions">
        <ThemeToggle />
        <Button
          variant="ghost"
          size="sm"
          onClick={handleSignOut}
          icon={<IconLogout />}
        >
          Encerrar sessao
        </Button>
      </div>
    </header>
  );
}

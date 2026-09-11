import { Link } from "react-router-dom";
import { IconArrowLeft } from "../components/icons";

export function NotFoundPage() {
  return (
    <div className="page page--narrow">
      <header className="page-header">
        <h1 className="page-header__title">Pagina nao encontrada</h1>
        <p className="page-header__description">
          O recurso solicitado nao existe ou foi movido.
        </p>
      </header>
      <Link to="/" className="link">
        <IconArrowLeft />
        <span>Voltar ao painel</span>
      </Link>
    </div>
  );
}

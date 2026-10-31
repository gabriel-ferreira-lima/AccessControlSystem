import { BrowserRouter, Route, Routes } from "react-router-dom";
import { AuthProvider } from "./context/AuthContext";
import { ThemeProvider } from "./context/ThemeContext";
import { RequireSession } from "./components/RequireSession";
import { RequireRole } from "./components/RequireRole";
import { AppShell } from "./components/layout/AppShell";
import { LoginPage } from "./pages/LoginPage";
import { HomePage } from "./pages/HomePage";
import { MyAccountPage } from "./pages/MyAccountPage";
import { OperatorsListPage } from "./pages/OperatorsListPage";
import { CreateOperatorPage } from "./pages/CreateOperatorPage";
import { EditOperatorPage } from "./pages/EditOperatorPage";
import { NotFoundPage } from "./pages/NotFoundPage";

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route
              element={
                <RequireSession>
                  <AppShell />
                </RequireSession>
              }
            >
              <Route path="/" element={<HomePage />} />
              <Route
                path="/minha-conta"
                element={
                  <RequireRole role="Operator">
                    <MyAccountPage />
                  </RequireRole>
                }
              />
              <Route
                path="/operadores"
                element={
                  <RequireRole role="Admin">
                    <OperatorsListPage />
                  </RequireRole>
                }
              />
              <Route
                path="/operadores/novo"
                element={
                  <RequireRole role="Admin">
                    <CreateOperatorPage />
                  </RequireRole>
                }
              />
              <Route
                path="/operadores/editar"
                element={
                  <RequireRole role="Admin">
                    <EditOperatorPage />
                  </RequireRole>
                }
              />
              <Route path="*" element={<NotFoundPage />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

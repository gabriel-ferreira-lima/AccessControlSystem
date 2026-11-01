import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ThemeProvider, useTheme } from "./ThemeContext";

// Componente sonda: expõe o tema atual em texto e o comando de alternar em um botão.
function Probe() {
  const { theme, toggleTheme } = useTheme();
  return (
    <div>
      <p>tema: {theme}</p>
      <button type="button" onClick={toggleTheme}>
        alternar
      </button>
    </div>
  );
}

function renderProbe() {
  render(
    <ThemeProvider>
      <Probe />
    </ThemeProvider>,
  );
  return userEvent.setup();
}

// O setup.ts define um matchMedia que sempre devolve "claro"; cada teste pode trocá-lo
// e o afterEach restaura o original.
const originalMatchMedia = window.matchMedia;

type ChangeListener = (event: { matches: boolean }) => void;

function mockMatchMedia(matches: boolean) {
  const listeners = new Set<ChangeListener>();
  const addEventListener = vi.fn((_type: string, listener: ChangeListener) => {
    listeners.add(listener);
  });
  const removeEventListener = vi.fn((_type: string, listener: ChangeListener) => {
    listeners.delete(listener);
  });
  window.matchMedia = vi.fn(() => ({
    matches,
    media: "(prefers-color-scheme: dark)",
    addEventListener,
    removeEventListener,
  })) as unknown as typeof window.matchMedia;
  return {
    addEventListener,
    removeEventListener,
    emitChange: (nextMatches: boolean) => {
      act(() => {
        listeners.forEach((listener) => listener({ matches: nextMatches }));
      });
    },
  };
}

afterEach(() => {
  window.matchMedia = originalMatchMedia;
  vi.restoreAllMocks();
});

describe("ThemeProvider", () => {
  it("usa o tema claro quando o sistema prefere claro e nada foi guardado", () => {
    renderProbe();

    expect(screen.getByText("tema: light")).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute("data-theme", "light");
  });

  it("usa o tema escuro quando o sistema prefere escuro e nada foi guardado", () => {
    mockMatchMedia(true);

    renderProbe();

    expect(screen.getByText("tema: dark")).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute("data-theme", "dark");
  });

  it("respeita o tema guardado em acs.theme, mesmo contra a preferência do sistema", () => {
    mockMatchMedia(true);
    window.localStorage.setItem("acs.theme", "light");

    renderProbe();

    expect(screen.getByText("tema: light")).toBeInTheDocument();
  });

  it("ignora um valor guardado que não seja light nem dark", () => {
    window.localStorage.setItem("acs.theme", "azul");

    renderProbe();

    expect(screen.getByText("tema: light")).toBeInTheDocument();
  });

  it("acompanha a mudança de preferência do sistema enquanto o operador não escolheu", () => {
    const media = mockMatchMedia(false);
    renderProbe();
    expect(screen.getByText("tema: light")).toBeInTheDocument();

    media.emitChange(true);

    expect(screen.getByText("tema: dark")).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute("data-theme", "dark");
  });

  it("deixa de acompanhar o sistema depois que o operador escolhe o tema", async () => {
    const media = mockMatchMedia(false);
    const user = renderProbe();

    await user.click(screen.getByRole("button", { name: "alternar" }));

    expect(screen.getByText("tema: dark")).toBeInTheDocument();
    expect(media.removeEventListener).toHaveBeenCalledWith("change", expect.any(Function));
    expect(window.localStorage.getItem("acs.theme")).toBe("dark");
  });

  it("alterna entre claro e escuro e guarda cada escolha", async () => {
    const user = renderProbe();

    await user.click(screen.getByRole("button", { name: "alternar" }));
    expect(screen.getByText("tema: dark")).toBeInTheDocument();
    expect(window.localStorage.getItem("acs.theme")).toBe("dark");

    await user.click(screen.getByRole("button", { name: "alternar" }));
    expect(screen.getByText("tema: light")).toBeInTheDocument();
    expect(window.localStorage.getItem("acs.theme")).toBe("light");
  });

  it("continua funcionando em memória quando o armazenamento está indisponível", async () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("armazenamento indisponível");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("armazenamento indisponível");
    });
    const user = renderProbe();
    expect(screen.getByText("tema: light")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "alternar" }));

    expect(screen.getByText("tema: dark")).toBeInTheDocument();
  });
});

describe("useTheme", () => {
  it("lança um erro claro quando usado fora do ThemeProvider", () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);

    expect(() => render(<Probe />)).toThrow("useTheme deve ser usado dentro de <ThemeProvider>.");
  });
});

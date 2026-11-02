// Este teste fica em src/lib/ porque é o diretório de testes de utilidades do front, mas ele NÃO testa uma função daqui:
// confere três arquivos de configuração da raiz do front (index.html, security-headers.conf e nginx.conf), por isso
// não existe um arquivo .ts irmão. Roda no jsdom padrão do projeto, com o src/test/setup.ts existente (que não é alterado).
//
// Contrato: specs/20261102-125831-security-perimeter/contracts/security-headers.md (FR-009 a FR-016).
import { describe, expect, it } from "vitest";
import indexHtml from "../../index.html?raw";
import nginxConf from "../../nginx.conf?raw";
import headersConf from "../../security-headers.conf?raw";

// Conteúdo do único <script> escrito na página (sem src). O navegador normaliza CRLF para LF ao ler o HTML antes de
// calcular o hash da CSP; no Windows o checkout deixa o arquivo em CRLF e no CI (Linux) em LF, então o hash correto é o do
// texto em LF nas duas plataformas.
function inlineScripts(html: string): string[] {
  const scripts: string[] = [];
  for (const match of html.matchAll(/<script(?![^>]*\ssrc\s*=)[^>]*>([\s\S]*?)<\/script>/gi)) {
    scripts.push(match[1].replace(/\r\n/g, "\n"));
  }
  return scripts;
}

async function sha256Base64(text: string): Promise<string> {
  const digest = await globalThis.crypto.subtle.digest("SHA-256", new TextEncoder().encode(text));
  let binary = "";
  for (const byte of new Uint8Array(digest)) binary += String.fromCharCode(byte);
  return btoa(binary);
}

// Sem comentários (#...) para as verificações não se enganarem com o texto explicativo.
function withoutComments(conf: string): string {
  return conf.replace(/\r\n/g, "\n").replace(/^\s*#.*$/gm, "").replace(/\s#.*$/gm, "");
}

function contentSecurityPolicy(): string {
  const match = /add_header\s+Content-Security-Policy\s+"([^"]*)"/.exec(withoutComments(headersConf));
  expect(match, "security-headers.conf não define o Content-Security-Policy").not.toBeNull();
  return match![1];
}

// Blocos "location ... { ... }" do nginx.conf (com as chaves balanceadas).
function locationBlocks(conf: string): { header: string; body: string }[] {
  const text = withoutComments(conf);
  const blocks: { header: string; body: string }[] = [];
  for (const start of text.matchAll(/location\s+([^{]+)\{/g)) {
    let depth = 1;
    let index = start.index! + start[0].length;
    const bodyStart = index;
    while (depth > 0 && index < text.length) {
      if (text[index] === "{") depth++;
      else if (text[index] === "}") depth--;
      index++;
    }
    blocks.push({ header: start[1].trim(), body: text.slice(bodyStart, index - 1) });
  }
  return blocks;
}

const INCLUDE = "include /etc/nginx/snippets/security-headers.conf;";
const SECURITY_HEADERS = [
  "Content-Security-Policy",
  "X-Content-Type-Options",
  "X-Frame-Options",
  "Referrer-Policy",
  "Permissions-Policy",
  "Strict-Transport-Security",
];

describe("cabeçalhos de segurança do console", () => {
  it("tem exatamente um script escrito na página (um segundo seria bloqueado pela CSP)", () => {
    expect(inlineScripts(indexHtml)).toHaveLength(1);
  });

  it("libera na CSP o hash do script do tema, e falha quando o script muda sem o hash acompanhar", async () => {
    const [script] = inlineScripts(indexHtml);
    const hash = await sha256Base64(script);

    expect(
      contentSecurityPolicy(),
      `O script do tema mudou. Atualize o security-headers.conf: troque o 'sha256-...' do script-src por 'sha256-${hash}'.`,
    ).toContain(`'sha256-${hash}'`);
  });

  it("não libera script escrito na página de forma genérica nem eval", () => {
    const csp = contentSecurityPolicy();

    expect(csp).not.toContain("unsafe-inline");
    expect(csp).not.toContain("unsafe-eval");
  });

  it("restringe origens, quadros, objetos, base e formulários ao próprio console", () => {
    const csp = contentSecurityPolicy();

    for (const directive of [
      "default-src 'self'",
      "script-src 'self'",
      "style-src 'self'",
      "img-src 'self' data:",
      "connect-src 'self'",
      "object-src 'none'",
      "frame-ancestors 'none'",
      "base-uri 'self'",
      "form-action 'self'",
    ]) {
      expect(csp, `CSP sem a diretiva ${directive}`).toContain(directive);
    }
  });

  it("define todos os cabeçalhos do conjunto, cada add_header com always", () => {
    const conf = withoutComments(headersConf);
    const lines = conf.split("\n").filter((line) => line.includes("add_header"));

    for (const header of SECURITY_HEADERS) {
      expect(lines.some((line) => line.includes(header)), `falta o add_header ${header}`).toBe(true);
    }
    for (const line of lines) {
      expect(line.trim(), `add_header sem always: ${line.trim()}`).toMatch(/\balways\s*;\s*$/);
    }
    expect(conf).toMatch(/add_header\s+X-Frame-Options\s+"DENY"/);
    expect(conf).toMatch(/add_header\s+X-Content-Type-Options\s+"nosniff"/);
    expect(conf).toMatch(/add_header\s+Referrer-Policy\s+"no-referrer"/);
  });

  it("emite HSTS de 1 ano, sem includeSubDomains nem preload", () => {
    const conf = withoutComments(nginxConf + "\n" + headersConf);

    expect(conf).toContain("max-age=31536000");
    expect(conf).not.toMatch(/includeSubDomains/i);
    expect(conf).not.toMatch(/preload/i);
    expect(conf).toMatch(/add_header\s+Strict-Transport-Security\s+\$hsts_value\s+always\s*;/);
  });

  it("só emite o HSTS quando X-Forwarded-Proto é https (map do nginx)", () => {
    const conf = withoutComments(nginxConf);

    expect(conf).toMatch(/map\s+\$http_x_forwarded_proto\s+\$hsts_value\s*\{[^}]*default\s+""\s*;[^}]*"?https"?\s+"max-age=31536000"\s*;[^}]*\}/);
  });

  it("inclui o trecho no server e em todo location que declara add_header", () => {
    const conf = withoutComments(nginxConf);
    const blocks = locationBlocks(nginxConf);
    let serverLevel = conf;
    for (const block of blocks) serverLevel = serverLevel.replace(block.body, "");

    expect(serverLevel, "o server precisa do include do trecho de cabeçalhos").toContain(INCLUDE);
    for (const block of blocks.filter((b) => b.body.includes("add_header"))) {
      expect(block.body, `o location ${block.header} tem add_header próprio e precisa repetir o include`).toContain(INCLUDE);
    }
  });

  it("esconde da resposta da Api os cabeçalhos que o nginx repõe (sem duplicata)", () => {
    const api = locationBlocks(nginxConf).find((block) => block.header.startsWith("/v1/"));
    expect(api, "nginx.conf sem o location /v1/").toBeDefined();

    for (const header of SECURITY_HEADERS) {
      expect(api!.body, `o /v1/ não esconde ${header}`).toMatch(new RegExp(`proxy_hide_header\\s+${header}\\s*;`));
    }
  });
});

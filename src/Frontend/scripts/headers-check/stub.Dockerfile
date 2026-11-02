# Upstream simulado (alias de rede "backend") para o nginx do console conseguir subir sem a Api real.
FROM nginx:1.27-alpine
COPY stub.conf /etc/nginx/conf.d/default.conf

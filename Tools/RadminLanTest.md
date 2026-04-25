# Test LAN con Radmin VPN

## Host

1. Abre Radmin VPN y crea/entra a la red.
2. Copia tu IP Radmin. Suele ser `26.x.x.x`.
3. Ejecuta `HOST_RADMIN.bat`.
4. Si Windows Firewall pregunta, permite redes privadas.
5. Pasa tu IP Radmin al amigo.

## Cliente

1. Entra a la misma red Radmin VPN.
2. Ejecuta `CLIENT_RADMIN.bat`.
3. Escribe la IP Radmin del host.
4. Firewall: permitir redes privadas si pregunta.

## Datos

- Puerto UDP: `7777`.
- Build local esperada: `Builds/Smoke/MarcoPoloSmoke/MarcoPoloSmoke.exe`.
- Logs:
  - `Builds/Smoke/radmin-host.log`
  - `Builds/Smoke/radmin-client.log`

## Si falla

- Probar `ping IP_RADMIN_DEL_HOST`.
- Host debe abrir primero.
- Ambos deben usar la misma build.
- No usar IP `192.168.x.x`; usar IP Radmin `26.x.x.x`.
- Permitir `.exe` en Windows Firewall.

(function () {
    "use strict";

    function initialize() {
        const container = document.querySelector("[data-solicitudes-realtime-container]");
        if (container === null) {
            return;
        }

        const statusIndicator = document.getElementById("solicitudesRealtimeStatusIndicator");
        const statusText = document.getElementById("solicitudesRealtimeStatusText");

        function setStatus(text, color) {
            if (statusText !== null) {
                statusText.textContent = text;
            }

            if (statusIndicator !== null) {
                statusIndicator.className = "badge rounded-pill bg-" + color;
                statusIndicator.textContent = text;
            }
        }

        if (!window.signalR) {
            setStatus("No disponible", "danger");
            return;
        }

        let reconnectTimer = null;
        let stopped = false;

        function getNotificationValue(notification, camelCaseName, pascalCaseName) {
            if (notification[camelCaseName] !== undefined) {
                return notification[camelCaseName];
            }

            return notification[pascalCaseName];
        }

        function normalizeEstado(value) {
            const estados = {
                Pendiente: "Pendiente",
                Aprobado: "Aprobado",
                Rechazado: "Rechazado",
                "0": "Pendiente",
                "1": "Aprobado",
                "2": "Rechazado"
            };

            return estados[String(value)] || String(value);
        }

        function updateEstadoBadge(badge, estado) {
            if (badge === null) {
                return;
            }

            badge.classList.remove("bg-warning", "text-dark", "bg-success", "bg-danger", "bg-secondary");

            const classes = {
                Pendiente: ["bg-warning", "text-dark"],
                Aprobado: ["bg-success"],
                Rechazado: ["bg-danger"]
            }[estado] || ["bg-secondary"];

            classes.forEach(className => badge.classList.add(className));
            badge.textContent = estado;
        }

        function showNotification(solicitudId, estado, motivoRechazo) {
            const notification = document.getElementById("solicitudesRealtimeNotification");
            if (notification === null) {
                return;
            }

            const estadoTexto = estado.toLowerCase();
            const motivo = estado === "Rechazado" && motivoRechazo
                ? ` Motivo: ${motivoRechazo}.`
                : "";

            notification.textContent = `La solicitud #${solicitudId} ahora está ${estadoTexto}.${motivo}`;
            notification.classList.remove("d-none", "alert-info", "alert-success", "alert-warning", "alert-danger");
            notification.classList.add(estado === "Rechazado" ? "alert-danger" : "alert-success");
        }

        function updateSolicitud(notification) {
            const solicitudId = Number(getNotificationValue(notification, "solicitudId", "SolicitudId"));
            if (!Number.isInteger(solicitudId) || solicitudId <= 0) {
                return;
            }

            const estado = normalizeEstado(getNotificationValue(notification, "estado", "Estado"));
            const motivoRechazo = getNotificationValue(notification, "motivoRechazo", "MotivoRechazo") || "";

            const row = document.querySelector(`tr[data-solicitud-id="${solicitudId}"]`);
            if (row !== null) {
                updateEstadoBadge(row.querySelector("[data-solicitud-estado]"), estado);
            }

            const detail = document.querySelector(`[data-solicitud-realtime-detail="${solicitudId}"]`);
            if (detail !== null) {
                updateEstadoBadge(detail.querySelector("[data-solicitud-estado]"), estado);

                const estadoTexto = detail.querySelector("[data-solicitud-estado-texto]");
                if (estadoTexto !== null) {
                    estadoTexto.textContent = estado;
                }

                const motivo = detail.querySelector("[data-solicitud-motivo-rechazo]");
                if (motivo !== null) {
                    motivo.textContent = estado === "Rechazado" && motivoRechazo
                        ? motivoRechazo
                        : "No aplica";
                }

                const estadoSelect = document.querySelector("[data-solicitud-estado-select]");
                if (estadoSelect !== null) {
                    estadoSelect.value = estado;
                }

                const motivoInput = document.querySelector("[data-solicitud-motivo-rechazo-input]");
                if (motivoInput !== null) {
                    motivoInput.value = estado === "Rechazado" ? motivoRechazo : "";
                }
            }

            showNotification(solicitudId, estado, motivoRechazo);
        }

        const connection = new window.signalR.HubConnectionBuilder()
            .withUrl("/hubs/solicitudes")
            .withAutomaticReconnect()
            .build();

        function scheduleReconnect() {
            if (stopped || reconnectTimer !== null) {
                return;
            }

            setStatus("Esperando reconexión...", "warning");
            reconnectTimer = window.setTimeout(() => {
                reconnectTimer = null;
                startConnection();
            }, 5000);
        }

        async function startConnection() {
            if (stopped || connection.state !== window.signalR.HubConnectionState.Disconnected) {
                return;
            }

            setStatus("Conectando...", "warning");
            try {
                await connection.start();
                setStatus("Conectado", "success");
            } catch (error) {
                setStatus("Sin conexión; reintentando...", "danger");
                scheduleReconnect();
            }
        }

        connection.onreconnecting(() => setStatus("Reconectando...", "warning"));
        connection.onreconnected(() => setStatus("Conectado", "success"));
        connection.onclose(() => {
            if (!stopped) {
                setStatus("Desconectado", "danger");
                scheduleReconnect();
            }
        });
        connection.on("SolicitudEstadoActualizado", updateSolicitud);

        window.addEventListener("beforeunload", () => {
            stopped = true;
            if (reconnectTimer !== null) {
                window.clearTimeout(reconnectTimer);
            }
            connection.stop();
        });

        startConnection();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize);
    } else {
        initialize();
    }
}());

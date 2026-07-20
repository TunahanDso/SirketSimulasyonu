from __future__ import annotations

import server


def main() -> None:
    print("=" * 56)
    print("İLOS TECH · Python Yazılım Şirketi Sunucusu")
    print(f"Şirket: {server.COMPANY_NAME} ({server.COMPANY_ID})")
    print(f"Sürüm: {server.SERVER_VERSION} · Port: {server.PORT}")
    print(f"Hizmet: {len(server.SERVICE_LIST)} · Uygulama: 2 · Protokol: 1")
    print("İSosyal · İMail · İLink motora kod tabanlı manifest olarak ilan edilir.")
    print("=" * 56)

    with server.ThreadedIlosServer(
        (server.HOST, server.PORT),
        server.IlosRequestHandler,
    ) as tcp_server:
        try:
            tcp_server.serve_forever(poll_interval=0.25)
        except KeyboardInterrupt:
            print("\nİlos Tech sunucusu kapatıldı.")


if __name__ == "__main__":
    main()

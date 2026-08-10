using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 방 코드로 호스트의 IP·포트를 찾아내는 LAN 디스커버리.
    ///
    /// <b>왜 Relay/Lobby 가 아닌가.</b> Unity Gaming Services 를 붙이면 코드 발급과 NAT 통과가
    /// 공짜로 따라오지만, 프로젝트 링크·인증·요금제라는 외부 의존이 생기고 서비스가 죽으면
    /// 로컬에서 친구끼리 하는 판까지 같이 죽는다. 지금 이 게임은 같은 집·같은 사무실에서
    /// 붙는 캐주얼 co-op 이므로, 의존 없이 UDP 한 소켓으로 끝나는 쪽을 고른다.
    ///
    /// <b>질의-응답 방향.</b> 호스트가 주기적으로 브로드캐스트를 뿌리는 방식이 흔하지만,
    /// 그러면 <b>클라이언트도 잘 알려진 포트에 바인드</b>해야 해서 한 PC 에서 호스트와
    /// 클라이언트를 같이 띄우는 순간(개발 중 가장 잦은 상황) 포트가 겹친다. 그래서 반대로
    /// 간다 — 잘 알려진 포트를 잡는 것은 호스트 하나뿐이고, 클라이언트는 임시 포트에서
    /// 질의를 던져 유니캐스트 응답을 받는다.
    /// </summary>
    public static class LastShiftRoomProtocol
    {
        /// <summary>게임 포트(<see cref="LastShiftNetworkSession.DefaultPort"/>) 바로 옆. 겹치지 않게 둔다.</summary>
        public const int DiscoveryPort = 7980;

        private const string Prefix = "LASTSHIFT/1";
        private const string Query = "QUERY";
        private const string Room = "ROOM";

        public static string BuildQuery(string code) => $"{Prefix} {Query} {code}";

        public static string BuildReply(string code, ushort gamePort) => $"{Prefix} {Room} {code} {gamePort}";

        public static bool TryParseQuery(string message, out string code)
        {
            code = null;
            var parts = Split(message, 3);
            if (parts == null || parts[1] != Query || !LastShiftRoomCode.IsValid(parts[2])) return false;
            code = parts[2];
            return true;
        }

        public static bool TryParseReply(string message, out string code, out ushort gamePort)
        {
            code = null;
            gamePort = 0;
            var parts = Split(message, 4);
            if (parts == null || parts[1] != Room || !LastShiftRoomCode.IsValid(parts[2])) return false;
            if (!ushort.TryParse(parts[3], out gamePort) || gamePort == 0) return false;
            code = parts[2];
            return true;
        }

        private static string[] Split(string message, int expected)
        {
            if (string.IsNullOrEmpty(message)) return null;
            var parts = message.Split(' ');
            if (parts.Length != expected || parts[0] != Prefix) return null;
            return parts;
        }
    }

    /// <summary>
    /// 호스트 쪽. 자기 방 코드를 묻는 질의에만 답한다. 다른 코드의 질의는 무시하므로,
    /// 같은 LAN 에 방이 여럿 떠 있어도 코드가 방을 가른다.
    /// </summary>
    public sealed class LastShiftRoomBeacon : IDisposable
    {
        private const int WindowsUdpConnectionReset = -1744830452; // SIO_UDP_CONNRESET

        private readonly UdpClient socket;
        private readonly string code;
        private readonly ushort gamePort;
        private volatile bool disposed;

        public string Code => code;
        public int DiscoveryPort { get; }

        public LastShiftRoomBeacon(string roomCode, ushort hostGamePort, int discoveryPort = LastShiftRoomProtocol.DiscoveryPort)
        {
            code = LastShiftRoomCode.Normalize(roomCode);
            if (!LastShiftRoomCode.IsValid(code))
                throw new ArgumentException($"Room code '{roomCode}' is not a valid LAST SHIFT room code.", nameof(roomCode));
            gamePort = hostGamePort;
            DiscoveryPort = discoveryPort;

            socket = new UdpClient { ExclusiveAddressUse = false };
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
            // Windows 는 앞서 보낸 유니캐스트 응답이 ICMP port unreachable 로 되돌아오면 그 뒤의
            // Receive 를 ConnectionReset 으로 깨운다. 방을 나간 클라이언트 하나 때문에 비컨이
            // 통째로 멈추므로 그 통지를 끈다. 다른 플랫폼에서는 지원하지 않으니 실패해도 넘어간다.
            try { socket.Client.IOControl(WindowsUdpConnectionReset, new byte[] { 0, 0, 0, 0 }, null); }
            catch (Exception) { /* 이 플랫폼에는 해당 제어 코드가 없다 */ }

            new Thread(Serve) { IsBackground = true, Name = "LastShiftRoomBeacon" }.Start();
        }

        private void Serve()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            var reply = Encoding.ASCII.GetBytes(LastShiftRoomProtocol.BuildReply(code, gamePort));
            while (!disposed)
            {
                byte[] payload;
                try
                {
                    payload = socket.Receive(ref remote);
                }
                catch (SocketException error)
                {
                    if (error.SocketErrorCode == SocketError.ConnectionReset) continue;
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (!LastShiftRoomProtocol.TryParseQuery(Encoding.ASCII.GetString(payload), out var asked)) continue;
                if (asked != code) continue;
                try { socket.Send(reply, reply.Length, remote); }
                catch (SocketException) { }
                catch (ObjectDisposedException) { return; }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Close 가 블로킹 중인 Receive 를 예외로 깨운다. 스레드는 그것을 보고 스스로 끝난다.
            try { socket.Close(); }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// 클라이언트 쪽. 코드를 뿌리고 응답한 호스트의 주소를 돌려준다.
    /// 응답의 <b>출발지 IP</b> 를 쓰기 때문에 호스트가 자기 IP 를 알 필요도, 알릴 필요도 없다.
    /// </summary>
    public static class LastShiftRoomResolver
    {
        private const int QueryIntervalMilliseconds = 250;

        public static bool TryResolve(
            string roomCode,
            int timeoutMilliseconds,
            out IPEndPoint endpoint,
            int discoveryPort = LastShiftRoomProtocol.DiscoveryPort)
        {
            endpoint = null;
            var code = LastShiftRoomCode.Normalize(roomCode);
            if (!LastShiftRoomCode.IsValid(code)) return false;

            using var socket = new UdpClient(0) { EnableBroadcast = true };
            socket.Client.ReceiveTimeout = QueryIntervalMilliseconds;
            var query = Encoding.ASCII.GetBytes(LastShiftRoomProtocol.BuildQuery(code));
            var targets = QueryTargets(discoveryPort);
            var remote = new IPEndPoint(IPAddress.Any, 0);
            var deadline = Environment.TickCount + timeoutMilliseconds;

            while (unchecked(Environment.TickCount - deadline) < 0)
            {
                foreach (var target in targets)
                {
                    try { socket.Send(query, query.Length, target); }
                    catch (SocketException) { /* 이 인터페이스로는 못 나간다 — 나머지로 계속 */ }
                }

                try
                {
                    var payload = socket.Receive(ref remote);
                    if (LastShiftRoomProtocol.TryParseReply(Encoding.ASCII.GetString(payload), out var replied, out var gamePort)
                        && replied == code)
                    {
                        endpoint = new IPEndPoint(remote.Address, gamePort);
                        return true;
                    }
                }
                catch (SocketException) { /* 이번 주기에는 응답이 없었다 */ }
                catch (ObjectDisposedException) { return false; }
            }

            return false;
        }

        /// <summary>
        /// 255.255.255.255 하나만 믿지 않는다. 어댑터에 따라 그 주소로 나간 패킷이 조용히
        /// 버려지는 경우가 있어, 인터페이스별 디렉티드 브로드캐스트도 같이 던진다.
        /// 루프백은 같은 PC 에서 호스트와 클라이언트를 띄우는 개발 중 경로를 보장한다.
        /// </summary>
        private static List<IPEndPoint> QueryTargets(int discoveryPort)
        {
            var targets = new List<IPEndPoint>
            {
                new(IPAddress.Loopback, discoveryPort),
                new(IPAddress.Broadcast, discoveryPort),
            };

            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (unicast.IPv4Mask == null) continue;
                        var directed = DirectedBroadcast(unicast.Address, unicast.IPv4Mask);
                        if (directed != null) targets.Add(new IPEndPoint(directed, discoveryPort));
                    }
                }
            }
            catch (NetworkInformationException)
            {
                // 어댑터 목록을 못 읽는 환경이면 위의 두 주소로만 시도한다.
            }

            return targets;
        }

        private static IPAddress DirectedBroadcast(IPAddress address, IPAddress mask)
        {
            var addressBytes = address.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            if (addressBytes.Length != 4 || maskBytes.Length != 4) return null;
            var broadcast = new byte[4];
            for (var index = 0; index < 4; index++)
                broadcast[index] = (byte)(addressBytes[index] | (byte)~maskBytes[index]);
            return new IPAddress(broadcast);
        }
    }

    /// <summary>
    /// <see cref="LastShiftRoomResolver.TryResolve"/> 는 응답이 올 때까지 스레드를 막는다.
    /// 메인 스레드에서 부르면 게임이 몇 초 얼어붙으므로, 조회는 배경 스레드에 두고
    /// 결과만 <see cref="Poll"/> 로 건져 간다 — Unity API 는 여전히 메인 스레드에서만 만진다.
    /// </summary>
    public sealed class LastShiftRoomLookup
    {
        private volatile bool finished;
        private volatile string foundAddress;
        private ushort foundPort;

        public string Code { get; }

        public LastShiftRoomLookup(string code, int timeoutMilliseconds, int discoveryPort = LastShiftRoomProtocol.DiscoveryPort)
        {
            Code = LastShiftRoomCode.Normalize(code);
            new Thread(() =>
            {
                string address = null;
                ushort port = 0;
                try
                {
                    if (LastShiftRoomResolver.TryResolve(Code, timeoutMilliseconds, out var endpoint, discoveryPort))
                    {
                        address = endpoint.Address.ToString();
                        port = (ushort)endpoint.Port;
                    }
                }
                catch (Exception error)
                {
                    Debug.LogWarning($"[LAST_SHIFT_ROOM] lookup code={Code} result=error detail={error.Message}");
                }

                foundPort = port;
                // finished 를 마지막에 세워, 이것을 본 메인 스레드는 주소도 함께 본다.
                foundAddress = address;
                finished = true;
            })
            { IsBackground = true, Name = "LastShiftRoomLookup" }.Start();
        }

        /// <summary>아직 찾는 중이면 false. 끝났으면 true 를 주고 결과를 채운다.</summary>
        public bool Poll(out string address, out ushort port)
        {
            address = null;
            port = 0;
            if (!finished) return false;
            address = foundAddress;
            port = foundPort;
            return true;
        }
    }
}

using System.Drawing;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CrewlinkSmo;

public class Program {
    private const ulong Magic = 0x416D6F6E67737573;
    private static int DataSize => Unsafe.SizeOf<Data>();
    private const int PingSize = 0x28;

    private static Socket Client = null!;
    private static UserInfo User;

    public static async Task Main(string[] args) {
        string name;
        IPAddress address;
        ushort port;
        if (args.Length >= 1) {
            address = IPAddress.Parse(args[0]);
        } else {
            Console.Write("Enter the IP address: ");
            while (true) {
                if (IPAddress.TryParse(Console.ReadLine() ?? "", out address!))
                    break;
                Console.WriteLine("Invalid port, try again: ");
            }
        }

        if (args.Length >= 2) {
            port = ushort.Parse(args[1]);
        } else {
            Console.Write("Enter the port: ");
            while (true) {
                if (ushort.TryParse(Console.ReadLine() ?? "", out port))
                    break;
                Console.WriteLine("Invalid port, try again: ");
            }
        }

        if (args.Length >= 3) {
            name = args[2];
        } else {
            Console.Write("Enter your name: ");
            name = Console.ReadLine()!;
            Console.WriteLine($"Entered {name}");
        }
        // yeah but like they all use windows so who cares! :)))))))
#pragma warning disable CA1416
        MemoryMappedFile memMap = MemoryMappedFile.OpenExisting("MumbleLink", MemoryMappedFileRights.FullControl,
            HandleInheritability.None);
#pragma warning restore CA1416
        using MemoryMappedViewAccessor mmf = memMap.CreateViewAccessor(0, 0xD54);
        byte[] data = Encoding.Unicode.GetBytes("Super Mario Odyssey");
        mmf.WriteArray(0x2C, data, 0, data.Length);
        data = Encoding.Unicode.GetBytes("Ain't no way -Mars2030");
        mmf.WriteArray(0x554, data, 0, data.Length);
        mmf.Write(0x0, 2);
        uint tick = 0;
        Vector3 zero = Vector3.Zero;
        Client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        await Client.ConnectAsync(new IPEndPoint(address, port));
        ReceiveUserInfo();
        SendPings(name);
        PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(25));
        while (true) {
            await timer.WaitForNextTickAsync();
            mmf.Write(0x4, unchecked(tick++));
            data = Encoding.Unicode.GetBytes(User.Id.ToString());
            mmf.WriteArray(0x250, data, 0, data.Length);
            mmf.Write(0x8, ref User.Position);
            mmf.Write(0x14, ref User.Front);
            // mmf.Write(0x20, ref zero);
            mmf.Write(0x22C, ref User.Position);
            mmf.Write(0x238, ref User.Front);
            // mmf.Write(0x244, ref zero);
            mmf.Write(0x450, sizeof(ulong));
            mmf.Write(0x454, User.LocationHash);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private static async void SendPings(string name) {
        PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        byte[] data = new byte[PingSize];

        void Setup() {
            Span<byte> span = data;
            ulong magic = Magic;
            MemoryMarshal.Write(span, ref magic);
            Encoding.UTF8.GetBytes(name).CopyTo(span[8..]);
        }

        Setup();
        while (true) {
            await timer.WaitForNextTickAsync();
            await Client.SendAsync(data.AsMemory(), SocketFlags.None);
            // Console.WriteLine("Sent ping");
        }
    }

    private static async void ReceiveUserInfo() {
        void Handling(Span<byte> span) {
            Data data = MemoryMarshal.Read<Data>(span);
            if (data.Magic != Magic) {
                Console.Error.WriteLine($"Incorrect magic got {data.Magic:x}, expected {Magic:x}!");
            }

            User.Id = data.Id;
            User.Position = data.Position;
            User.Front = ToEulerAngles(data.Rotation);
            User.LocationHash = data.LocationHash;
            Console.WriteLine(User.LocationHash);
        }

        byte[] data = new byte[DataSize];
        while (true) {
            // Console.WriteLine("Now waiting for packet");
            int bytesReceived = await Client.ReceiveAsync(data, SocketFlags.None);
            if (bytesReceived != DataSize) {
                await Console.Error.WriteLineAsync($"Received incomplete buffer of size {bytesReceived}, expecting {DataSize}");
                continue;
            }

            Handling(data);
        }
    }

    // https://stackoverflow.com/a/70462919
    private static Vector3 ToEulerAngles(Quaternion q) {
        Vector3 angles = new Vector3();

        // roll / x
        double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
        double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
        angles.X = (float) Math.Atan2(sinr_cosp, cosr_cosp);

        // pitch / y
        double sinp = 2 * (q.W * q.Y - q.Z * q.X);
        if (Math.Abs(sinp) >= 1) {
            angles.Y = (float) Math.CopySign(Math.PI / 2, sinp);
        } else {
            angles.Y = (float) Math.Asin(sinp);
        }

        // yaw / z
        double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
        double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
        angles.Z = (float) Math.Atan2(siny_cosp, cosy_cosp);

        return Vector3.Normalize(angles);
    }

    private struct Data {
        public ulong Magic;
        public Guid Id;
        public Vector3 Position;
        public Quaternion Rotation;
        public ulong LocationHash;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct Balls {
        public uint Version;
        public uint Tick;
        public Vector3 Position;
        public Vector3 Front;
        public Vector3 Top;
        public WCharPad Pad;
        public Vector3 CamPosition;
        public Vector3 CamFront;
        public Vector3 CamTop;
    }

    [StructLayout(LayoutKind.Auto, Size = 512)]
    private struct WCharPad {
        public char FirstChar;
        public Span<char> Data => MemoryMarshal.CreateSpan(ref FirstChar, 256);
    }
}
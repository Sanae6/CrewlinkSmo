using System.Numerics;
using System.Runtime.InteropServices;

namespace CrewlinkSmo;

public struct UserInfo {
    public Guid Id;
    public Vector3 Position;
    public Vector3 Front;
    public ulong LocationHash;
    // public Vector3 AvatarTop;
    // public Vector3 CameraPosition;
    // public Vector3 CameraFront;
    // public Vector3 CameraTop;
}
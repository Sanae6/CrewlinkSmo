using System.Numerics;
using System.Runtime.InteropServices;

namespace CrewlinkSmo;

public struct UserInfo {
    public Guid Id;
    public Vector3 Position;
    public Vector3 Front;
    public Vector3 LocationOffset;
}
using System.Runtime.InteropServices;

namespace Audora.Core.Diagnostics;

/// <summary>Reads the Plug and Play problem code of a device. Read only: it changes nothing.</summary>
public static class PnpStatus
{
    private const uint Success = 0;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint devInst, uint flags);

    /// <summary>Returns the problem code (0 means no problem), or null when Windows could not tell.</summary>
    public static int? ProblemCode(string instanceId)
    {
        try
        {
            if (CM_Locate_DevNodeW(out var node, instanceId, 0) != Success)
            {
                return null;
            }

            if (CM_Get_DevNode_Status(out _, out var problem, node, 0) != Success)
            {
                return null;
            }

            return (int)problem;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

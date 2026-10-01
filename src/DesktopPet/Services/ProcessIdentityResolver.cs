using System.Runtime.InteropServices;

namespace DesktopPet.Services;

/// <summary>
/// Membaca sumber identitas proses dari Windows: judul jendela top-level dan command line.
/// Tidak membaca memori proses lain; hanya API dengan hak PROCESS_QUERY_LIMITED_INFORMATION.
/// </summary>
public static class ProcessIdentityResolver
{
    /// <summary>
    /// Satu kali EnumWindows untuk memetakan PID ke judul jendela top-level yang terlihat.
    /// Urutan EnumWindows mengikuti z-order, jadi jendela yang paling depan menang.
    /// </summary>
    public static Dictionary<int, string> SnapshotWindowTitles()
    {
        var titles = new Dictionary<int, string>();
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
            {
                return true;
            }

            int length = NativeMethods.GetWindowTextLength(hWnd);
            if (length <= 0) return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (titles.ContainsKey((int)pid)) return true;

            var buffer = new char[length + 1];
            int copied = NativeMethods.GetWindowText(hWnd, buffer, buffer.Length);
            if (copied > 0)
            {
                titles[(int)pid] = new string(buffer, 0, copied);
            }
            return true;
        }, IntPtr.Zero);
        return titles;
    }

    public static string? TryReadCommandLine(int pid)
    {
        IntPtr handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;

        try
        {
            NativeMethods.NtQueryInformationProcess(handle, NativeMethods.ProcessCommandLineInformation, IntPtr.Zero, 0, out int needed);
            if (needed <= 0 || needed > 1 << 20) return null;

            IntPtr buffer = Marshal.AllocHGlobal(needed);
            try
            {
                if (NativeMethods.NtQueryInformationProcess(handle, NativeMethods.ProcessCommandLineInformation, buffer, needed, out _) != 0)
                {
                    return null;
                }

                var str = Marshal.PtrToStructure<NativeMethods.UNICODE_STRING>(buffer);
                if (str.Buffer == IntPtr.Zero || str.Length == 0) return null;
                return Marshal.PtrToStringUni(str.Buffer, str.Length / 2);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    public static int? TryReadParentPid(int pid)
    {
        IntPtr handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;

        try
        {
            var info = new NativeMethods.PROCESS_BASIC_INFORMATION();
            int status = NativeMethods.NtQueryInformationProcess(handle, NativeMethods.ProcessBasicInformation, ref info,
                Marshal.SizeOf<NativeMethods.PROCESS_BASIC_INFORMATION>(), out _);
            return status == 0 ? (int)info.InheritedFromUniqueProcessId : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    /// <summary>
    /// Membaca info peluncuran proses sekali saat ditemukan. Command line utuh tidak disimpan.
    /// </summary>
    public static ProcessLaunchInfo ReadLaunchInfo(int pid)
    {
        var args = ProcessNameFormatter.SplitCommandLine(TryReadCommandLine(pid));
        return new ProcessLaunchInfo(
            ProcessNameFormatter.ExtractCommandLineContext(args),
            ProcessNameFormatter.IsHelperCommandLine(args),
            TryReadParentPid(pid));
    }
}

public sealed record ProcessLaunchInfo(string? CommandLineContext, bool IsHelperByCommandLine, int? ParentPid);

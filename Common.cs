using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ToSAddonManager {
    internal static class Common {
        internal static void showError(string funcName, Exception ex) {
            MessageBox.Show(ex.Message, funcName, MessageBoxButton.OK, MessageBoxImage.Error);
        } // end showError

        internal static bool checkForToSProcess() {
            bool ret = false;
            try {
                System.Diagnostics.Process[] p = System.Diagnostics.Process.GetProcessesByName("Client_tos");
                if (p.Length != 0) { ret = true; }
            } catch (Exception ex) {
                Common.showError("Check For ToS Process", ex);
            }
            return ret;
        } // end checkForToSProcess

        internal static bool checkForToSDirectory(string tosRootDir) {
            bool ret = false;
            if (!string.IsNullOrEmpty(tosRootDir) && System.IO.Directory.Exists(tosRootDir)) {
                System.Security.Permissions.FileIOPermission fP = new System.Security.Permissions.FileIOPermission(System.Security.Permissions.FileIOPermissionAccess.Read, tosRootDir);
                fP.AddPathList(System.Security.Permissions.FileIOPermissionAccess.Write | System.Security.Permissions.FileIOPermissionAccess.Read, tosRootDir);
                try {
                    fP.Demand();
                    ret = true;
                } catch (System.Security.SecurityException ex) {
                    Common.showError("Invalid access to directory", ex);
                }
            }
            return ret;
        } // end checkForToSDirectory
    }
}

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
    }
}

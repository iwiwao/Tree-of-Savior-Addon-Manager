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
    }
}

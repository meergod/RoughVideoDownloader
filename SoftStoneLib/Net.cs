using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace SoftStone.Net {
  public static class Utils {
    public static string recvMsgFromChrome() {
      var buffer = new byte[4];
      using(var stdin = Console.OpenStandardInput()) {
        stdin.Read(buffer, 0, 4);
        var msgLen = BitConverter.ToInt32(buffer, 0);
        buffer = new byte[msgLen];
        stdin.Read(buffer, 0, msgLen);
      }
      return Encoding.UTF8.GetString(buffer);
    }

    public static void sendMsgToChrome(object value) {
      var bytes = Encoding.UTF8.GetBytes(
        new JavaScriptSerializer().Serialize(value));
      using(var stdout = Console.OpenStandardOutput()) {
        stdout.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
        stdout.Write(bytes, 0, bytes.Length);
      }
    }
  }
}

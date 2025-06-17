
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace InRang
{
    /// <summary>
    /// 게임의 전역 설정을 관리하는 정적 클래스
    /// </summary>
    public static class GameSettings
    {
        // 기본 게임 설정
        public static int PlayerCount = 8;      // 전체 플레이어 수
        public static int AICount = 7;          // AI 플레이어 수
        public static bool YaminabeMode = false; // 야미나베 모드
        public static bool QuantumMode = false;  // 양자인랑 모드


        // 🔹 전역 IP 주소 설정    => StartPageForm에서 클라이언트의 ip주소로 변경될 것임
        public static string ServerIP = "127.0.0.1";    // 기본 서버 IP
        public static string LocalIP = "127.0.0.1";     // 기본 로컬 IP

        public static string UserName = "Player";
    }
}
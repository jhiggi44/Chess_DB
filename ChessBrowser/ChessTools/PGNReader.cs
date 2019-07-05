using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ChessTools {
    public class PGNReader {
        List<string> lines = new List<string>();

        public PGNReader(string fileName) {
            string[] temp = File.ReadAllLines(fileName);
            foreach (string t in temp) {
                string line = t;
                if (line.Length > 0 && line[0] == '[') {
                    line = t.Remove(0, 1);
                }
                lines.Add(line);
            }
        }
        
        public List<ChessGame> extractGameData() {
            List<ChessGame> games = new List<ChessGame>();
            string ev, site, date, wPlayer, bPlayer, res, moves;
            int wElo, bElo;
            int i = 0;
            while (i < lines.Count) {
                ev = site = date = wPlayer = bPlayer = res = moves = "";
                bElo = wElo = 0;
                while (!string.IsNullOrWhiteSpace(lines[i])) {
                    if (lines[i].StartsWith("Event ")) {
                        ev = extractSubStr(lines[i]);
                    } else if (lines[i].StartsWith("Site ")) {
                        site = extractSubStr(lines[i]);
                    } else if (lines[i].StartsWith("White ")) {
                        wPlayer = extractSubStr(lines[i]);
                    } else if (lines[i].StartsWith("Black ")) {
                        bPlayer = extractSubStr(lines[i]);
                    } else if (lines[i].StartsWith("Result ")) {
                        string resStr = extractSubStr(lines[i]);
                        if (resStr.Contains("0-1")) {
                            res = "B";
                        } else if (resStr.Contains("1-0")) {
                            res = "W";
                        } else {
                            res = "D";
                        }
                    } else if (lines[i].StartsWith("WhiteElo ")) {
                        string eloStr = extractSubStr(lines[i]);
                        wElo = Convert.ToInt32(eloStr);
                    } else if (lines[i].StartsWith("BlackElo ")) {
                        string eloStr = extractSubStr(lines[i]);
                        bElo = Convert.ToInt32(eloStr);
                    } else if (lines[i].StartsWith("EventDate ")) {
                        date = extractSubStr(lines[i]);
                    }
                    i++;
                }

                i++;

                while (!string.IsNullOrWhiteSpace(lines[i])) {
                    moves += lines[i];
                    i++;
                }
                i++;
                games.Add(new ChessGame(ev, site, date, wPlayer, bPlayer, wElo, bElo, res, moves));
            }
            return games;
        }

        private string extractSubStr(string line) {
            int start = line.IndexOf('"') + 1;
            return line.Substring(start, (line.Length - 2) - start);
        }

    }
}

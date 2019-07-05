using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessTools
{
    public class ChessGame {
        public string Event, Site, EventDate, WhitePlayer, BlackPlayer, Result, Moves;
        public int WhiteElo, BlackElo;
        public ChessGame(string ev, string site, string date, string wPlayer, string bPlayer, int wElo, int bElo, string res, string moves) {
            Event = ev;
            Site = site;
            EventDate = date;
            WhitePlayer = wPlayer;
            BlackPlayer = bPlayer;
            WhiteElo = wElo;
            BlackElo = bElo;
            Result = res;
            Moves = moves;
        }

        public void print() {
            Console.WriteLine("Event: " + Event + "\nSite: " + Site + "\nEventDate: " + EventDate);
            Console.WriteLine("WhitePlayer: " + WhitePlayer + "\nBlackPlayer: " + BlackPlayer + "\nWinner: " + Result);
            Console.WriteLine("WhiteElo: " + WhiteElo + "\nBlackElo: " + BlackElo + "\nMoves: " + Moves + "\n\n");
        }
    }
}

using ChessTools;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ChessBrowser {
    partial class Form1 {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Keep track of which radio button is pressed
        /// </summary>
        private RadioButton winnerButton = null;

        /// <summary>
        /// This function handles the "Upload PGN" button.
        /// Given a filename, parses the PGN file, and uploads
        /// each chess game to the user's database.
        /// </summary>
        /// <param name="PGNfilename">The path to the PGN file</param>
        private void UploadGamesToDatabase(string PGNfilename) {
            // This will build a connection string to your user's database on atr,
            // assuimg you've typed a user and password in the GUI
            string connection = GetConnectionString();

            // Load and parse the PGN file
            PGNReader pgn = new PGNReader(PGNfilename);
            List<ChessGame> games = pgn.extractGameData();

            // Use this to tell the GUI's progress bar how many total work steps there are
            // For example, one iteration of your main upload loop could be one work step
            SetNumWorkItems(games.Count);

            Console.WriteLine(games.Count + " total games...");

            using (MySqlConnection conn = new MySqlConnection(connection)) {
                try {
                    // Open a connection
                    conn.Open();

                    int i = 1;
                    // TODO: iterate through your data and generate appropriate insert commands
                    foreach (ChessGame g in games) {
                        insertIntoEvents(conn, g);
                        insertIntoPlayers(conn, g);
                        insertIntoGames(conn, g);
                        // Use this to tell the GUI that one work step has completed:
                        WorkStepCompleted();
                        Console.WriteLine(i + " game finished");
                        i++;
                    }
                }
                catch (Exception e) {
                    Console.WriteLine(e.Message);
                }
            }

        }

        /// <summary>
        /// This function inserts a Game object into the Events table of the database.
        /// </summary>
        /// <param name="conn">The connection to the database</param>
        /// <param name="game">The game to add to the Events table</param>
        private void insertIntoEvents(MySqlConnection conn, ChessGame game) {
            using (MySqlCommand comm = new MySqlCommand("select Name, Date from Events where Name = @Name and Date = @Date", conn)) {
                comm.Parameters.AddWithValue("@Name", game.Event);
                comm.Parameters.AddWithValue("@Date", game.EventDate);
                using (MySqlDataReader reader = comm.ExecuteReader()) {
                    if (!reader.HasRows) {
                        reader.Close();
                        //Console.WriteLine("new event");
                        comm.CommandText = "insert ignore into Events (Name, Site, Date) values (@Name, @Site, @Date)";
                        comm.Parameters.AddWithValue("@Site", game.Site);
                        comm.ExecuteNonQuery();
                    }
                    else {
                        reader.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Given a player's name, this function gets the corresponding PlayerID from the Players table.
        /// </summary>
        /// <param name="conn">The connection to the database</param>
        /// <param name="name">The name of the player</param>
        private int getPlayerID(MySqlConnection conn, string name) {
            using (MySqlCommand comm = new MySqlCommand("select pID from Players where Name = @Name", conn)) {
                comm.Parameters.AddWithValue("@Name", name);
                using (MySqlDataReader reader = comm.ExecuteReader()) {
                    reader.Read();
                    int ret = reader.GetInt32(0);
                    reader.Close();
                    return ret;
                }
            }
        }

        /// <summary>
        /// This function inserts a Game object into the Games table of the database.
        /// </summary>
        /// <param name="conn">The connection to the database</param>
        /// <param name="game">The game to add to the Games table</param>
        private void insertIntoGames(MySqlConnection conn, ChessGame game) {
            // check Events table and get eID
            using (MySqlCommand comm = new MySqlCommand("select eID from Events where Name = @Name and Date = @Date", conn)) {
                comm.Parameters.AddWithValue("@Name", game.Event);
                comm.Parameters.AddWithValue("@Date", game.EventDate);
                using (MySqlDataReader reader = comm.ExecuteReader()) {
                    if (reader.HasRows) {
                        reader.Read();
                        int eID = reader.GetInt32(0);
                        reader.Close();
                        int bpID = getPlayerID(conn, game.BlackPlayer);
                        int wpID = getPlayerID(conn, game.WhitePlayer);
                        comm.CommandText = "select * from Games where eID = @eID and BlackPlayer = @BlackPlayer and WhitePlayer = @WhitePlayer";
                        comm.Parameters.AddWithValue("@eID", eID);
                        comm.Parameters.AddWithValue("@BlackPlayer", bpID);
                        comm.Parameters.AddWithValue("@WhitePlayer", wpID);
                        using (MySqlDataReader reader2 = comm.ExecuteReader()) {
                            if (!reader2.HasRows) {
                                reader2.Close();
                                comm.CommandText = "insert into Games (Result, Moves, BlackPlayer, WhitePlayer, eID) values (@Result, @Moves, @BlackPlayer, @WhitePlayer, @eID)";
                                comm.Parameters.AddWithValue("@Result", game.Result);
                                comm.Parameters.AddWithValue("@Moves", game.Moves);
                                comm.ExecuteNonQuery();
                            }
                            reader2.Close();
                        }
                    }
                    else {
                        reader.Close();
                        Console.WriteLine("No Event eID...\n");
                    }
                }
            }
        }

        /// <summary>
        /// This function adds a player to the Players table. Can handle both black and white players. 
        /// </summary>
        /// <param name="conn">The connection to the database</param>
        /// <param name="player">The player to add to the Players table</param>
        /// <param name="elo">The elo rating of the player</param>
        private void addPlayer(MySqlConnection conn, string player, int elo) {
            using (MySqlCommand comm = new MySqlCommand("select Elo from Players where Name = @Name", conn)) {
                comm.Parameters.AddWithValue("@Name", player);
                using (MySqlDataReader reader = comm.ExecuteReader()) {
                    if (!reader.HasRows) {
                        reader.Close();
                        comm.CommandText = "insert ignore into Players (Name, Elo) values (@Name, @Elo)";
                        comm.Parameters.AddWithValue("@Elo", elo);
                        comm.ExecuteNonQuery();
                    }
                    else {
                        reader.Read();
                        if (reader.GetInt32(0) < elo) {
                            reader.Close();
                            using (MySqlCommand nonQuery = new MySqlCommand("update Players p set Elo = @Elo where p.Name = @Name", conn)) {
                                nonQuery.Parameters.AddWithValue("@Elo", elo);
                                nonQuery.Parameters.AddWithValue("@Name", player);
                                nonQuery.ExecuteNonQuery();
                            }
                        }
                        reader.Close();
                    }
                }
            }
        }

        /// <summary>
        /// This function adds both the players (black and white) from a game to the Players table.
        /// </summary>
        /// <param name="conn">The connection to the database</param>
        /// <param name="game">The game to add to the Players table</param>
        private void insertIntoPlayers(MySqlConnection conn, ChessGame game) {
            addPlayer(conn, game.BlackPlayer, game.BlackElo);
            addPlayer(conn, game.WhitePlayer, game.WhiteElo);
        }



        /// <summary>
        /// Queries the database for games that match all the given filters.
        /// The filters are taken from the various controls in the GUI.
        /// </summary>
        /// <param name="white">The white player, or "" if none</param>
        /// <param name="black">The black player, or "" if none</param>
        /// <param name="opening">The first move, e.g. "e4", or "" if none</param>
        /// <param name="winner">The winner as "White", "Black", "Draw", or "" if none</param>
        /// <param name="useDate">True if the filter includes a date range, False otherwise</param>
        /// <param name="start">The start of the date range</param>
        /// <param name="end">The end of the date range</param>
        /// <param name="showMoves">True if the returned data should include the PGN moves</param>
        /// <returns>A string separated by windows line endings ("\r\n") containing the filtered games</returns>
        private string PerformQuery(string white, string black, string opening,
      string winner, bool useDate, DateTime start, DateTime end, bool showMoves) {
            // This will build a connection string to your user's database on atr,
            // assuimg you've typed a user and password in the GUI
            string connection = GetConnectionString();

            // Build up this string containing the results from your query
            string parsedResult = "";

            // Use this to count the number of rows returned by your query
            // (see below return statement)
            int numRows = 0;

            using (MySqlConnection conn = new MySqlConnection(connection)) {
                try {
                    // Open a connection
                    conn.Open();

                    string query = "select EventName, Site, Date, WhiteName, black.Name as BlackName, Result from "
                    + "(select games.Name as EventName, games.Site, games.Date, w.Name as WhiteName, games.BlackPlayer, games.Result from "
                    + "(select * from Games g natural join Events e ";
                    if (showMoves) {
                        query = "select EventName, Site, Date, WhiteName, black.Name as BlackName, Result, Moves from "
                    + "(select games.Name as EventName, games.Site, games.Date, w.Name as WhiteName, games.BlackPlayer, games.Result, games.Moves from "
                    + "(select * from Games g natural join Events e ";
                    }

                    using (MySqlCommand comm = new MySqlCommand(query, conn)) {
                        bool isFirst = true;
                        if (winner != "" || useDate || showMoves) {
                            if (winner != "") {
                                comm.CommandText += "where g.Result = @Result ";
                                comm.Parameters.AddWithValue("@Result", winner.Substring(0, 1));
                                isFirst = false;
                            }
                            if (useDate) {
                                if (isFirst) {
                                    comm.CommandText += "where e.Date >= @Start and e.Date <= @End ";
                                    isFirst = false;
                                } else {
                                    comm.CommandText += "and e.Date >= @Start and e.Date <= @End ";
                                }
                                comm.Parameters.AddWithValue("@End", end);
                                comm.Parameters.AddWithValue("@Start", start);
                            }
                            if (showMoves) { 
                                if (isFirst) {
                                    comm.CommandText += "where g.Moves like @Move ";
                                } else {
                                    comm.CommandText += "and g.Moves like @Move ";
                                }
                                
                                comm.Parameters.AddWithValue("@Move", opening + "%");
                            }
                        }

                        comm.CommandText += ") games join Players w where w.pID = games.WhitePlayer ";
                        if (white != "") {
                            comm.CommandText += "and w.Name = @WhitePlayer";
                            comm.Parameters.AddWithValue("@WhitePlayer", white);
                        }

                        comm.CommandText += ") white join Players black where black.pID = white.BlackPlayer";
                        if (black != "") {
                            comm.CommandText += " and black.Name = @BlackPlayer";
                            comm.Parameters.AddWithValue("@BlackPlayer", black);
                        }

                        using (MySqlDataReader reader = comm.ExecuteReader()) {
                            while (reader.Read()) {
                                parsedResult += "Event: " + reader[0] + "\r\n";
                                parsedResult += "Site: " + reader[1] + "\r\n";
                                parsedResult += "Date: " + reader[2] + "\r\n";
                                parsedResult += "White: " + reader[3] + "\r\n";
                                parsedResult += "Black: " + reader[4] + "\r\n";
                                parsedResult += "Result: " + reader[5] + "\r\n";
                                if (showMoves) {
                                    parsedResult += "Moves: " + reader[6] + "\r\n";
                                }
                                parsedResult += "\r\n";
                                numRows++;
                            }
                        }
                    }
                }
                catch (Exception e) {
                    Console.WriteLine(e.Message);
                }
            }

            return numRows + " results\r\n\r\n" + parsedResult;
        }


        /// <summary>
        /// Informs the progress bar that one step of work has been completed.
        /// Use SetNumWorkItems first
        /// </summary>
        private void WorkStepCompleted() {
            backgroundWorker1.ReportProgress(0);
        }

        /// <summary>
        /// Informs the progress bar how many steps of work there are.
        /// </summary>
        /// <param name="x">The number of work steps</param>
        private void SetNumWorkItems(int x) {
            this.Invoke(new MethodInvoker(() => {
                uploadProgress.Maximum = x;
                uploadProgress.Step = 1;
            }));
        }

        /// <summary>
        /// Reads the username and password from the text fields in the GUI
        /// and puts them into an SQL connection string for the atr server.
        /// </summary>
        /// <returns></returns>
        private string GetConnectionString() {
            return "server=atr.eng.utah.edu;database=" + userText.Text + ";uid=" + userText.Text + ";password=" + pwdText.Text;
        }

        /***************GUI functions below this point***************/
        /*You should not need to directly use any of these functions*/

        /// <summary>
        /// Disables the two buttons on the GUI,
        /// so that only one task can happen at once
        /// </summary>
        private void DisableControls() {
            uploadButton.Enabled = false;
            goButton.Enabled = false;
        }

        /// <summary>
        /// Enables the two buttons on the GUI, used after a task completes.
        /// </summary>
        private void EnableControls() {
            uploadButton.Enabled = true;
            goButton.Enabled = true;
        }


        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.userText = new System.Windows.Forms.TextBox();
            this.pwdText = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.uploadButton = new System.Windows.Forms.Button();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.uploadProgress = new System.Windows.Forms.ProgressBar();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.whitePlayerText = new System.Windows.Forms.TextBox();
            this.blackPlayerText = new System.Windows.Forms.TextBox();
            this.startDate = new System.Windows.Forms.DateTimePicker();
            this.label6 = new System.Windows.Forms.Label();
            this.endDate = new System.Windows.Forms.DateTimePicker();
            this.dateCheckBox = new System.Windows.Forms.CheckBox();
            this.whiteWin = new System.Windows.Forms.RadioButton();
            this.label5 = new System.Windows.Forms.Label();
            this.blackWin = new System.Windows.Forms.RadioButton();
            this.drawWin = new System.Windows.Forms.RadioButton();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.openingMoveText = new System.Windows.Forms.TextBox();
            this.resultText = new System.Windows.Forms.TextBox();
            this.showMovesCheckBox = new System.Windows.Forms.CheckBox();
            this.goButton = new System.Windows.Forms.Button();
            this.anyRadioButton = new System.Windows.Forms.RadioButton();
            this.SuspendLayout();
            // 
            // userText
            // 
            this.userText.Location = new System.Drawing.Point(62, 12);
            this.userText.Name = "userText";
            this.userText.Size = new System.Drawing.Size(142, 26);
            this.userText.TabIndex = 0;
            // 
            // pwdText
            // 
            this.pwdText.Location = new System.Drawing.Point(341, 12);
            this.pwdText.Name = "pwdText";
            this.pwdText.PasswordChar = '*';
            this.pwdText.Size = new System.Drawing.Size(140, 26);
            this.pwdText.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(43, 20);
            this.label1.TabIndex = 2;
            this.label1.Text = "User";
            this.label1.Click += new System.EventHandler(this.label1_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(257, 13);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(78, 20);
            this.label2.TabIndex = 3;
            this.label2.Text = "Password";
            // 
            // uploadButton
            // 
            this.uploadButton.Location = new System.Drawing.Point(62, 82);
            this.uploadButton.Name = "uploadButton";
            this.uploadButton.Size = new System.Drawing.Size(149, 50);
            this.uploadButton.TabIndex = 4;
            this.uploadButton.Text = "Upload PGN";
            this.uploadButton.UseVisualStyleBackColor = true;
            this.uploadButton.Click += new System.EventHandler(this.button1_Click);
            // 
            // backgroundWorker1
            // 
            this.backgroundWorker1.WorkerReportsProgress = true;
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker1_ProgressChanged);
            this.backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);
            // 
            // uploadProgress
            // 
            this.uploadProgress.Location = new System.Drawing.Point(261, 82);
            this.uploadProgress.Name = "uploadProgress";
            this.uploadProgress.Size = new System.Drawing.Size(900, 50);
            this.uploadProgress.TabIndex = 5;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(17, 236);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(97, 20);
            this.label3.TabIndex = 6;
            this.label3.Text = "White Player";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(290, 236);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(95, 20);
            this.label4.TabIndex = 7;
            this.label4.Text = "Black Player";
            // 
            // whitePlayerText
            // 
            this.whitePlayerText.Location = new System.Drawing.Point(116, 236);
            this.whitePlayerText.Name = "whitePlayerText";
            this.whitePlayerText.Size = new System.Drawing.Size(145, 26);
            this.whitePlayerText.TabIndex = 8;
            // 
            // blackPlayerText
            // 
            this.blackPlayerText.Location = new System.Drawing.Point(387, 236);
            this.blackPlayerText.Name = "blackPlayerText";
            this.blackPlayerText.Size = new System.Drawing.Size(145, 26);
            this.blackPlayerText.TabIndex = 9;
            // 
            // startDate
            // 
            this.startDate.Enabled = false;
            this.startDate.Location = new System.Drawing.Point(161, 319);
            this.startDate.Name = "startDate";
            this.startDate.Size = new System.Drawing.Size(300, 26);
            this.startDate.TabIndex = 11;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(468, 324);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(64, 20);
            this.label6.TabIndex = 12;
            this.label6.Text = "through";
            // 
            // endDate
            // 
            this.endDate.Enabled = false;
            this.endDate.Location = new System.Drawing.Point(538, 320);
            this.endDate.Name = "endDate";
            this.endDate.Size = new System.Drawing.Size(300, 26);
            this.endDate.TabIndex = 13;
            // 
            // dateCheckBox
            // 
            this.dateCheckBox.AutoSize = true;
            this.dateCheckBox.Location = new System.Drawing.Point(21, 320);
            this.dateCheckBox.Name = "dateCheckBox";
            this.dateCheckBox.Size = new System.Drawing.Size(131, 24);
            this.dateCheckBox.TabIndex = 14;
            this.dateCheckBox.Text = "Filter By Date";
            this.dateCheckBox.UseVisualStyleBackColor = true;
            this.dateCheckBox.CheckedChanged += new System.EventHandler(this.dateCheckBox_CheckedChanged);
            // 
            // whiteWin
            // 
            this.whiteWin.AutoSize = true;
            this.whiteWin.Location = new System.Drawing.Point(873, 237);
            this.whiteWin.Name = "whiteWin";
            this.whiteWin.Size = new System.Drawing.Size(75, 24);
            this.whiteWin.TabIndex = 15;
            this.whiteWin.TabStop = true;
            this.whiteWin.Text = "White";
            this.whiteWin.UseVisualStyleBackColor = true;
            this.whiteWin.CheckedChanged += new System.EventHandler(this.whiteWin_CheckedChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(801, 239);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(59, 20);
            this.label5.TabIndex = 16;
            this.label5.Text = "Winner";
            // 
            // blackWin
            // 
            this.blackWin.AutoSize = true;
            this.blackWin.Location = new System.Drawing.Point(954, 237);
            this.blackWin.Name = "blackWin";
            this.blackWin.Size = new System.Drawing.Size(73, 24);
            this.blackWin.TabIndex = 17;
            this.blackWin.TabStop = true;
            this.blackWin.Text = "Black";
            this.blackWin.UseVisualStyleBackColor = true;
            this.blackWin.CheckedChanged += new System.EventHandler(this.blackWin_CheckedChanged);
            // 
            // drawWin
            // 
            this.drawWin.AutoSize = true;
            this.drawWin.Location = new System.Drawing.Point(1033, 237);
            this.drawWin.Name = "drawWin";
            this.drawWin.Size = new System.Drawing.Size(71, 24);
            this.drawWin.TabIndex = 18;
            this.drawWin.TabStop = true;
            this.drawWin.Text = "Draw";
            this.drawWin.UseVisualStyleBackColor = true;
            this.drawWin.CheckedChanged += new System.EventHandler(this.drawWin_CheckedChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(17, 180);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(168, 20);
            this.label7.TabIndex = 19;
            this.label7.Text = "Find games filtered by:";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(559, 236);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(111, 20);
            this.label8.TabIndex = 20;
            this.label8.Text = "Opening Move";
            // 
            // openingMoveText
            // 
            this.openingMoveText.Location = new System.Drawing.Point(677, 235);
            this.openingMoveText.Name = "openingMoveText";
            this.openingMoveText.Size = new System.Drawing.Size(100, 26);
            this.openingMoveText.TabIndex = 21;
            // 
            // resultText
            // 
            this.resultText.Location = new System.Drawing.Point(17, 446);
            this.resultText.Multiline = true;
            this.resultText.Name = "resultText";
            this.resultText.ReadOnly = true;
            this.resultText.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.resultText.Size = new System.Drawing.Size(1144, 654);
            this.resultText.TabIndex = 22;
            // 
            // showMovesCheckBox
            // 
            this.showMovesCheckBox.AutoSize = true;
            this.showMovesCheckBox.Location = new System.Drawing.Point(17, 387);
            this.showMovesCheckBox.Name = "showMovesCheckBox";
            this.showMovesCheckBox.Size = new System.Drawing.Size(125, 24);
            this.showMovesCheckBox.TabIndex = 23;
            this.showMovesCheckBox.Text = "Show Moves";
            this.showMovesCheckBox.UseVisualStyleBackColor = true;
            // 
            // goButton
            // 
            this.goButton.BackColor = System.Drawing.Color.Silver;
            this.goButton.Location = new System.Drawing.Point(148, 377);
            this.goButton.Name = "goButton";
            this.goButton.Size = new System.Drawing.Size(133, 42);
            this.goButton.TabIndex = 24;
            this.goButton.Text = "Go!";
            this.goButton.UseVisualStyleBackColor = false;
            this.goButton.Click += new System.EventHandler(this.button1_Click_1);
            // 
            // anyRadioButton
            // 
            this.anyRadioButton.AutoSize = true;
            this.anyRadioButton.Checked = true;
            this.anyRadioButton.Location = new System.Drawing.Point(1110, 237);
            this.anyRadioButton.Name = "anyRadioButton";
            this.anyRadioButton.Size = new System.Drawing.Size(59, 24);
            this.anyRadioButton.TabIndex = 25;
            this.anyRadioButton.TabStop = true;
            this.anyRadioButton.Text = "any";
            this.anyRadioButton.UseVisualStyleBackColor = true;
            this.anyRadioButton.CheckedChanged += new System.EventHandler(this.radioButton1_CheckedChanged);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1185, 1112);
            this.Controls.Add(this.anyRadioButton);
            this.Controls.Add(this.goButton);
            this.Controls.Add(this.showMovesCheckBox);
            this.Controls.Add(this.resultText);
            this.Controls.Add(this.openingMoveText);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.drawWin);
            this.Controls.Add(this.blackWin);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.whiteWin);
            this.Controls.Add(this.dateCheckBox);
            this.Controls.Add(this.endDate);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.startDate);
            this.Controls.Add(this.blackPlayerText);
            this.Controls.Add(this.whitePlayerText);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.uploadProgress);
            this.Controls.Add(this.uploadButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.pwdText);
            this.Controls.Add(this.userText);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox userText;
        private System.Windows.Forms.TextBox pwdText;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button uploadButton;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.ProgressBar uploadProgress;
        private Label label3;
        private Label label4;
        private TextBox whitePlayerText;
        private TextBox blackPlayerText;
        private DateTimePicker startDate;
        private Label label6;
        private DateTimePicker endDate;
        private CheckBox dateCheckBox;
        private RadioButton whiteWin;
        private Label label5;
        private RadioButton blackWin;
        private RadioButton drawWin;
        private Label label7;
        private Label label8;
        private TextBox openingMoveText;
        private TextBox resultText;
        private CheckBox showMovesCheckBox;
        private Button goButton;
        private RadioButton anyRadioButton;
    }
}


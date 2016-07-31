using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MinecraftClient.ChatBots
{
    class Quiz : ChatBot
    {
        public enum Level {baby,easy,normal,hard,extreme}
        public List<Question> questions = new List<Question>();
        public List<PlayerStat> playerStats;
        public List<AskingSession> runningSessions = new List<AskingSession>();
        public int givenTime = 30;
        public int maxTopNames = 5;
        public int maxQuestionSize = 80;
        public string statFileName = "playerStats.txt";
        public string defaultQuestionFileName = "questions.txt";
        public string customQuestionFileName = "myQuestions.txt";

        public Random rnd = new Random();


        public string CATEGORY_TAG = "Category:";
        public string QUESTION_TAG = "Question:";
        public string ANSWER_TAG = "Answer:";
        public string REGEXP_TAG = "Regexp:";
        public string LEVEL_TAG = "Level:";

        public string NON_EXISTANT_NAME = "nonExistant";

        public class PlayerStat
        {
            public string playerName;
            public int total;
            public int right;

            public void incTotal()
            {
                total++;
            }

            public void incRight()
            {
                right++;
            }

            public static int compareByRight(PlayerStat x, PlayerStat y)
            {
                if (x.right > y.right) return -1;
                if (x.right < y.right) return 1;
                return 0;
            }
        }

        public struct Question
        {
            public string category;
            public string question;
            public string answer;
            public Regex Regexp;
            public Level level;
        }

        public class Session
        {
            public Session(DateTime startTime,string player)
            {
                this.startTime = startTime;
                this.player = player;
            }

            
            public DateTime startTime;
            public string player;
        }

        public class AskingSession : Session
        {
            public AskingSession(Question question, DateTime startTime, string player) : base(startTime, player)
            {               
                this.question = question;
            }
            public Question question;

        }


        public override void Initialize()
        {
            readQuestionFile(defaultQuestionFileName);
            //LogToConsole(questions.Count);
            //LogToConsole(questions.ElementAt(1).question);
            if(!File.Exists(statFileName))
            {
                File.Create(statFileName);
            }
            loadPlayerStats();
        }



        public override void Update()
        {
            List<AskingSession> removeList = new List<AskingSession>();
            foreach(AskingSession session in runningSessions)
            {
                if (DateTime.Now.Subtract(session.startTime) >= TimeSpan.FromSeconds(givenTime))
                {
                    removeList.Add(session);
                    SendPrivateMessage(session.player, "Richtig war: " + session.question.answer);
                    addPlayerStats(session.player, false);
                }
            }
            runningSessions= runningSessions.Except(removeList).ToList();
        }

        public override void GetText(string text)
        {
            string player = "";
            string message = "";

            if(IsPrivateMessage(text, ref message, ref player))
            {
                if(message.ToLower().Contains("askme"))
                {
                    foreach(Session session in runningSessions)
                    {
                        if(session.player.Equals(player))
                        {
                            return;
                        }
                    }
                    Question temp = selectQuestion();
                    runningSessions.Add(new AskingSession(temp, DateTime.Now, player));
                    SendPrivateMessage(player, temp.question);
                }

                if(message.ToLower().Contains("mystats"))
                {
                    PlayerStat stat = getPlayerStats(player);
                    if (!stat.playerName.Equals(NON_EXISTANT_NAME))
                    {
                        SendPrivateMessage(player, 
                            "Du hast " + stat.right + " von " + stat.total + " Fragen richtig beantwortet.");
                    } else
                    {
                        SendPrivateMessage(player, "Du hast noch keine Fragen beantwortet.");
                    }
                }

                if(message.ToLower().Contains("top"))
                {
                    playerStats.Sort(PlayerStat.compareByRight);
                    string reply = "";
                    for(int i = 0; i < maxTopNames; i++)
                    {
                        if (playerStats.Count <= i) break;
                        reply = reply + playerStats.ElementAt(i).playerName + "(" + playerStats.ElementAt(i).right + ") ";
                    }
                    SendPrivateMessage(player, reply);
                }

                AskingSession it;
                for (int i = runningSessions.Count - 1; i >= 0; i--)
                {
                    it = runningSessions[i];
                    if (player.Equals(it.player) &&
                        isRightAnswer(message, it.question))
                    {
                        SendPrivateMessage(player, "Richtig!");
                        addPlayerStats(player, true);
                        runningSessions.RemoveAt(i);
                    }
                }

            }
        }

        public String getEntryFromLine(String line, String type)
        {
            String entry = line.Substring(type.Length);

            while(entry.StartsWith(" "))
            {
                entry = entry.Remove(0, 1);
            }
            return entry;
        }


        public Level getLevelFromString(string str)
        {
            switch(str) {
                case "baby": return Level.baby;
                case "easy": return Level.easy;
                case "normal": return Level.normal;
                case "hard": return Level.hard;
                case "extreme": return Level.extreme;
                default: return Level.normal;
            }
        }

        protected string[] LoadEntriesFromFile(string file)
        {
            if (File.Exists(file))
            {
                //Read all lines from file, remove lines with no text, convert to a string array, and return the result.
                return File.ReadAllLines(file)
                        .Where(line => !String.IsNullOrWhiteSpace(line))
                        .ToArray();
            }
            else
            {
                LogToConsole("File not found: " + Settings.Alerts_MatchesFile);
                return new string[0];
            }
        }

        public bool isRightAnswer(string message, Question question)
        {
            if(message.ToLower().Equals(question.answer.ToLower()))
            {
                return true;
            }
            if(question.Regexp != null &&
                question.Regexp.IsMatch(message))
            {
                return true;
            }
            return false;
        }

        public Question selectQuestion()
        {
            Question q;
            do
            {
                q = questions.ElementAt(rnd.Next(0, questions.Count));
            }
            while (q.question.Length > maxQuestionSize);
            return q;
        }



        public void addPlayerStats(string player,bool wasRight)
        {
            for(int i = 0; i < playerStats.Count; i++)
            {
                LogToConsole(playerStats.ElementAt(i).playerName + " " + player);
                if(playerStats.ElementAt(i).playerName.Equals(player))
                {
                    playerStats.ElementAt(i).incTotal();
                    if (wasRight) playerStats.ElementAt(i).incRight();
                    savePlayerStats();
                    return;
                }
            }

            PlayerStat stat = new PlayerStat();
            stat.playerName = player;
            stat.total = 1;
            if (wasRight) stat.right = 1;
            else stat.right = 0;
            playerStats.Add(stat);
            savePlayerStats();
        }

        public PlayerStat getPlayerStats(string player)
        {
            foreach(PlayerStat stat in playerStats)
            {
                //LogToConsole(stat.playerName + " " + player);
                if(stat.playerName.Equals(player))
                {
                    return stat;
                }
            }
            PlayerStat wrongStat = new PlayerStat();
            wrongStat.playerName = NON_EXISTANT_NAME;
            wrongStat.total = -1;
            wrongStat.right = -1;
            return wrongStat;
        }

        public void loadPlayerStats()
        {
            string[] lines = File.ReadAllLines(statFileName);
            string pattern = "^([A-Za-z0-9_]+): ([0-9]+):([0-9]+)$";
            playerStats = new List<PlayerStat>();
            for (int i = 0; i < lines.Length; i++)
            {
                
                if(Regex.IsMatch(lines[i], pattern ))
                {
                    Match match = Regex.Match(lines[i], pattern);
                    PlayerStat stat = new PlayerStat();
                    /*
                    foreach(Group gr in match.Groups)
                    {
                        LogToConsole(gr.Value);
                    }
                    */
                    stat.playerName = match.Groups[1].Value;
                    stat.total = Convert.ToInt16(match.Groups[2].Value);
                    stat.right = Convert.ToInt16(match.Groups[3].Value);
                    LogToConsole(stat.playerName);
                    playerStats.Add(stat);
                }
            }
        }

        public void savePlayerStats()
        {
            string[] str = new string[playerStats.Count];
            PlayerStat stat;
            for ( int i = 0; i < playerStats.Count; i++)
            {
                stat = playerStats.ElementAt(i);
                str[i] = stat.playerName + ": " + stat.total + ":" + stat.right;
            }
            File.WriteAllLines(statFileName,str);
        }

        public void readQuestionFile(string fileName)
        {
            string[] rawQuestions = LoadEntriesFromFile(fileName);
            LogToConsole(rawQuestions.Length);
            Question temp = new Question();
            string line;
            for (int i = 0; i < rawQuestions.Length; i++)
            {
                line = rawQuestions[i];
                if (line.StartsWith("#")) continue;

                if (line.StartsWith(CATEGORY_TAG))
                {
                    //Letzte Frage abschließen und neue beginnen
                    questions.Add(temp);
                    temp = new Question();

                    temp.category = getEntryFromLine(line, CATEGORY_TAG);
                }

                if (line.StartsWith(QUESTION_TAG))
                {
                    if (temp.question != null)
                    {
                        //Letzte Frage abschließen und neue beginnen
                        questions.Add(temp);
                        temp = new Question();
                    }


                    temp.question = getEntryFromLine(line, QUESTION_TAG);
                }

                if (line.StartsWith(ANSWER_TAG))
                {
                    temp.answer = getEntryFromLine(line, ANSWER_TAG).Replace("#", "");
                }

                if (line.StartsWith(REGEXP_TAG))
                {
                    Regex regexp = new Regex(getEntryFromLine(line, REGEXP_TAG));
                    temp.Regexp = regexp;
                }

                if (line.StartsWith(LEVEL_TAG))
                {
                    temp.level = getLevelFromString(getEntryFromLine(line, LEVEL_TAG));
                }
            }
            questions.Add(temp);
        }
    }
}

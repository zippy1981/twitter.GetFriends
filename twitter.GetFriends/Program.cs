using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Kent.Boogaart.KBCsv;
using MongoDB.Bson;
using Twitterizer;
using twitter.GetFriends.Properties;

namespace twitter.GetFriends
{
    internal sealed class Program
    {
        private static OAuthTokens _tokens;

        static Program()
        {
            _tokens = new OAuthTokens
            {
                ConsumerKey = Settings.Default.ConsumerKey,
                ConsumerSecret = Settings.Default.ConsumerSecret
            };
        }
        
        private static void Main()
        {
            if (Settings.Default.UseAccessPair)
            {
                _tokens.AccessToken = Settings.Default.AccessToken;
                _tokens.AccessTokenSecret = Settings.Default.AccessSecret;
            }
            else
            {
                if (string.IsNullOrEmpty(Settings.Default.UserAccessToken) || string.IsNullOrEmpty(Settings.Default.UserAccessSecret))
                {
                    var tokenResponse = GetUserToken();
                    Settings.Default.UserAccessToken = tokenResponse.Token;
                    Settings.Default.UserAccessSecret = tokenResponse.TokenSecret;
                    Settings.Default.UserId = (int)tokenResponse.UserId;
                    Settings.Default.Save();
                }
                _tokens.AccessToken = Settings.Default.UserAccessToken;
                _tokens.AccessTokenSecret = Settings.Default.UserAccessSecret;
            }

            /*
            {
                var userInfo = GetUserInfo().ToBsonDocument();
                var csvWriter = new CsvWriter(Console.Out);
                csvWriter.WriteHeaderRecord(userInfo.Names.ToArray());
                csvWriter.WriteDataRecord(userInfo.Values);
                Console.WriteLine();
            }
            */
            {
                var users = GetMutualFriends(_tokens);
                var csvWriter = new CsvWriter(Console.Out);
                csvWriter.WriteHeaderRecord(users.First().ToBsonDocument().Names.ToArray());
                foreach (var user in users) { csvWriter.WriteDataRecord(user.ToBsonDocument().Values.ToArray()); }
                Console.WriteLine();
            }
            
            Console.Write("Press any key to continue . . .");
            Console.ReadKey(false);
            Console.WriteLine();
        }

        private static TwitterUser GetUserInfo()
        {
            var lookupOptions = new LookupUsersOptions();
            lookupOptions.UserIds.Add(Settings.Default.UserId);
            lookupOptions.UseSSL = true;

            var userInfo = TwitterUser.Lookup(_tokens, lookupOptions);
            return userInfo.ResponseObject.First();
        }

        private static IEnumerable<TwitterUser> GetMutualFriends(OAuthTokens tokens)
        {
            var friends = TwitterFriendship.FriendsIds(tokens);
            var followers = TwitterFriendship.FollowersIds(tokens);

            var userIds = (from
                               friendId in friends.ResponseObject
                           join
                               followerId in followers.ResponseObject
                               on friendId equals followerId
                           select friendId).ToArray();

            var lookupOptions = new LookupUsersOptions();
            var users = new List<TwitterUser>();
            for (int i = 0; i < userIds.Length; i ++)
            {
                lookupOptions.UserIds.Add(userIds[i]);
                if (lookupOptions.UserIds.Count() >= 100 || i + 1 == userIds.Length)
                {
                    users.AddRange(TwitterUser.Lookup(tokens, lookupOptions).ResponseObject);
                    lookupOptions.UserIds.Clear();
                }
            }
            return users;
        }

        private static OAuthTokenResponse GetUserToken()
        {
            var token = OAuthUtility.GetRequestToken(Settings.Default.ConsumerKey, Settings.Default.ConsumerSecret, "oob");
            Process.Start("http://twitter.com/oauth/authorize?oauth_token=" + token.Token);

            Console.Write("Please authorize our application and enter the pin code: ");
            var pin = ReadPassword();
            Console.WriteLine();

            return OAuthUtility.GetAccessToken(Settings.Default.ConsumerKey, Settings.Default.ConsumerSecret, token.Token, pin);
        }

        /// <summary>
        /// Reads a password from the console entering asterisk (*) instead of the character on the screen.
        /// </summary>
        /// <returns>The string entered</returns>
        /// <remarks>Taken from <a href="http://www.c-sharpcorner.com/Forums/Thread/32102/password-in-C-Sharp-console-application.aspx">this csharp corner thread.</a></remarks>
        private static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key != ConsoleKey.Backspace)
                {
                    Console.Write("*");
                    password += info.KeyChar;
                }
                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        password = password.Substring(0, password.Length - 1);
                        int pos = Console.CursorLeft;
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        Console.Write(" ");
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                    }
                }
                info = Console.ReadKey(true);
            }

            return password;
        }
    }
}

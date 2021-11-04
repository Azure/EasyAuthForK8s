using MessagePack;
using System;
using System.Collections.Generic;

namespace EasyAuthForK8s.Web.Models
{
    /// <summary>
    /// optional identifying information about the user that can be shared with the back-end application
    /// data is compressed before it is encrypted into the cookie, which could introduce security vulnerabilities
    /// </summary>
    [MessagePackObject]
    public class UserInfoPayload
    {
        [Key(0)]
        public string name { get; set; } = string.Empty;
        [Key(1)]
        public string oid { get; set; } = string.Empty;
        [Key(2)]
        public string preferred_username { get; set; } = string.Empty;
        [Key(3)]
        public List<string> roles { get; set; } = new List<string>();
        [Key(4)]
        public string sub { get; set; } = string.Empty;
        [Key(5)]
        public string tid { get; set; } = string.Empty;
        [Key(6)]
        public string email { get; set; } = string.Empty;
        [Key(7)]
        public string groups { get; set; } = string.Empty;
        [Key(8)]
        public List<KeyValuePair<string, string>> otherClaims { get; set; } = new List<KeyValuePair<string, string>>();
        [Key(9)]
        public List<string> graph { get; set; } = new List<string>();
    }
}

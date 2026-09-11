using System;
using System.Collections.Generic;
using System.Text;

namespace AccessControlSystem.Application {
    public static class Configuration {

        public static DatabaseConfiguration Database { get; set; } = new();
        public static SecretsConfiguration Secrets { get; set; } = new();

        public class DatabaseConfiguration {
            public string ConnectionString { get; set; } = string.Empty;
        }
        public class SecretsConfiguration {
            public string JwtPrivateKey { get; set; } = string.Empty;
            public string PasswordSaltKey { get; set; } = string.Empty;
        }
    }
}

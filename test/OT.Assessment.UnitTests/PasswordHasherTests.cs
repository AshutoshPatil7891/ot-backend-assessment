using OT.Assessment.App.Services;

namespace OT.Assessment.UnitTests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void CorrectPassword_Verifies()
        {
            var hash = PasswordHasher.Hash("secret1");

            Assert.True(PasswordHasher.Verify("secret1", hash));
        }

        [Fact]
        public void WrongPassword_DoesNotVerify()
        {
            var hash = PasswordHasher.Hash("secret1");

            Assert.False(PasswordHasher.Verify("secret2", hash));
        }

        [Fact]
        public void SamePasswordTwice_GivesDifferentHashes()
        {
            Assert.NotEqual(PasswordHasher.Hash("secret1"), PasswordHasher.Hash("secret1"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-hash")]
        [InlineData("%%%:%%%")]
        public void BadStoredValue_DoesNotVerifyAndDoesNotThrow(string stored)
        {
            Assert.False(PasswordHasher.Verify("secret1", stored));
        }

        [Fact]
        public void HashDoesNotContainThePlainPassword()
        {
            Assert.DoesNotContain("secret1", PasswordHasher.Hash("secret1"));
        }
    }
}

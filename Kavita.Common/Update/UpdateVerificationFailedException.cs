namespace Kavita.Common.Update
{
    public class UpdateVerificationFailedException : KavitaException
    {
        public UpdateVerificationFailedException(string message, params object[] args)
            : base(message, args)
        {
        }
        public UpdateVerificationFailedException(string message) : base(message)
        {

        }
    }
}
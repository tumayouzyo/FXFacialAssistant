using System;

namespace TY.FXFacialAssistant
{
    internal enum FacialProblem
    {
        Unspecified, SaveName, SaveFolder, Avatar, Mesh, LoadClip, RecordingSelection, DefaultClip, Preview, Fx
    }

    internal static class FacialErrors
    {
        private const string Key = "TY.FXFacialAssistant.Problem";
        public static ArgumentException Argument(FacialProblem problem, string message)
            => Tag(new ArgumentException(message), problem);
        public static InvalidOperationException Operation(FacialProblem problem, string message)
            => Tag(new InvalidOperationException(message), problem);
        private static T Tag<T>(T exception, FacialProblem problem) where T : Exception
        {
            exception.Data[Key] = problem;
            return exception;
        }
        public static FacialProblem Problem(Exception exception)
            => exception.Data[Key] is FacialProblem problem ? problem : FacialProblem.Unspecified;
    }
}

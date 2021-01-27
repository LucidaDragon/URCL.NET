﻿using System.Collections.Generic;

namespace SpeedAsm
{
    public interface ILinePreparser
    {
        /// <summary>
        /// Called at the beginning of a parsing session.
        /// </summary>
        void Begin(Parser parser);
        /// <summary>
        /// Called when an instruction is generated during a parsing session.
        /// </summary>
        /// <param name="parser">The current parser.</param>
        /// <param name="inst">The instruction that was generated.</param>
        void Generated(Parser parser, Instruction inst);
        /// <summary>
        /// Called at the end of a parsing session.
        /// </summary>
        void End(Parser parser);
        /// <summary>
        /// Attempt to use the preprocessor to parse the specified line.
        /// </summary>
        /// <param name="parser">The current parser.</param>
        /// <param name="line">The line to parse.</param>
        /// <param name="result">The resulting instructions, if successful.</param>
        /// <returns>Whether the preparser successfully parsed the line.</returns>
        bool TryParse(Parser parser, string line, out IEnumerable<Instruction> result);
    }
}

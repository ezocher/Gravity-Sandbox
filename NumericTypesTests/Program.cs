﻿using System;
using System.Drawing;


namespace NumericTypesTests
{
    class Program
    {
        static void Main(string[] args)
        {
            const long Million = 1000000;

            //FloatingPointAdditionPrecision.CAP_SpecificExample();

            // GoldenRatioNumericIssueDemo.RunPsiCalculations(30);
            //Console.WriteLine("\n---------------\n");
            //FloatingPointNotAssociative.Demonstration();
            //Console.WriteLine("\n---------------\n");
            //FloatingPointAdditionPrecision.Investigations();
            //Console.WriteLine("\n---------------\n");
            //FloatingPointUtilPerformanceTests.RunTest(100 * Million);

            ISSOrbitScenario scenario = new ISSOrbitScenario();
            scenario.RunISSOrbits();
        }
    }
}

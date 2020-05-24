﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace GravitySandboxUWP
{
    public class Flatbody
    {

        // TBD: refactor to SimSpace
        // Mass is in abstract units and is designed to make interesting accelerations happen at the scale and speed of the simulation
        //      Requested masses are scaled by the massFactor, so a requested mass of 1.0 is 100,000 mass
        private const double massFactor = 100000.0; // TBD: refactor to SimSpace
        private const double defaultMass = 1.0;

        double mass;
        public double Mass
        {
            get { return mass; }
            private set { mass = value * massFactor; }
        }

        // Size is in abstract units, with 1.0 the smallest size normally rendered
        private const double defaultSize = 2.0; // TBD: refactor to SimSpace
        public double Size { get; private set; }

        Point position;
        public Point Position
        {
            get { return position; }
            private set { position = value; }
        }

        private Point defaultStartingVelocity = new Point(0.0, 0.0);
        private Point velocity;
        public Point Velocity
        {
            get { return velocity; }
            private set { velocity = value; }
        }

        // GravitySource is true for bodies that exert gravity on all other bodies and false for those that don't
        //    exert gravity on any other bodies
        //
        // Usage: For scenarios with many tiny bodies orbiting a large one (e.g. satellites in earth orbit), 
        //   have only the larger body exert gravity. This will eliminate unnecessary calculations and also leave the large
        //   body undisturbed by all the tiny things flying around it
        private const bool defaultGravitySource = true;
        public bool IsGravitySource { get; private set; }

        #region Constructors
        public Flatbody(Point bodyStartingPosition)
        {
            Mass = defaultMass;
            Size = defaultSize;
            Position = bodyStartingPosition;
            Velocity = defaultStartingVelocity;
            IsGravitySource = defaultGravitySource;
        }

        public Flatbody(double bodyMass, double bodySize, Point bodyStartingPosition)
        {
            Mass = bodyMass;
            Size = bodySize;
            Position = bodyStartingPosition;
            Velocity = defaultStartingVelocity;
            IsGravitySource = defaultGravitySource;
        }

        public Flatbody(double bodyMass, double bodySize, Point bodyStartingPosition, Point bodyStartingVelocity)
        {
            Mass = bodyMass;
            Size = bodySize;
            Position = bodyStartingPosition;
            Velocity = bodyStartingVelocity;
            IsGravitySource = defaultGravitySource;
        }

        public Flatbody(double bodyMass, double bodySize, Point bodyStartingPosition, Point bodyStartingVelocity,
            bool isGravitySource)
        {
            Mass = bodyMass;
            Size = bodySize;
            Position = bodyStartingPosition;
            Velocity = bodyStartingVelocity;
            IsGravitySource = isGravitySource;
        }
        #endregion

        #region 2D Physics

        // TBD: Look holistically at distance minimums and acceleration limits and clean up and centralize
        // Need to keep at least some minimum value for r to avoid divide by zero
        public Point BodyToBodyAccelerate(Flatbody otherBody)
        {
            const double rMinimum = 10.0;   // we are not simulating collisions so don't let accelerations run away as bodies
                                           //  approach 0.0 separation
            const double rMinSquared = rMinimum * rMinimum;

            double rX = otherBody.position.X - position.X;
            double rY = otherBody.position.Y - position.Y;

            double rSquared = (rX * rX) + (rY * rY);
            rSquared = Math.Max(rSquared, rMinSquared); // enforce minimum value of r
            double r = Math.Sqrt(rSquared);

            // TBD: refactor to SimSpace (currently omits g)
            // F = m1 * a, g = m1 * m2 / rSquared, m1's cancel out so a = m2 / rSquared
            double a = otherBody.mass / rSquared;

            return (new Point(a * rX / r, a * rY / r));
        }

        public void Move(Point accel, double deltaT)
        {
            if ( !((accel.X == 0.0) && (accel.Y == 0.0)) )
            {
                // Apply linear acceleration during the time interval

                double newVelocityX = velocity.X + (accel.X * deltaT);
                position.X += (velocity.X + newVelocityX) / 2 * deltaT;
                velocity.X = newVelocityX;

                double newVelocityY = velocity.Y + (accel.Y * deltaT);
                position.Y += (velocity.Y + newVelocityY) / 2 * deltaT;
                velocity.Y = newVelocityY;
            }
        }

        #endregion
    }
}

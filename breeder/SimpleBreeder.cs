﻿using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace com.alphatica.genotick.breeder
{

	using Instruction = com.alphatica.genotick.instructions.Instruction;
	using InstructionList = com.alphatica.genotick.instructions.InstructionList;
	using TerminateInstructionList = com.alphatica.genotick.instructions.TerminateInstructionList;
	using Mutator = com.alphatica.genotick.mutator.Mutator;
	using Population = com.alphatica.genotick.population.Population;
	using Robot = com.alphatica.genotick.population.Robot;
	using RobotInfo = com.alphatica.genotick.population.RobotInfo;


	public class SimpleBreeder : RobotBreeder
	{
		private BreederSettings settings;
		private Mutator mutator;

		public static RobotBreeder Instance
		{
			get
			{
				return new SimpleBreeder();
			}
		}

		public virtual void breedPopulation(Population population, IList<RobotInfo> robotInfos)
		{
			if (population.haveSpaceToBreed())
			{
				addRequiredRandomRobots(population);
				breedPopulationFromParents(population, robotInfos);
				addOptionalRandomRobots(population);
			}
		}

		private void addOptionalRandomRobots(Population population)
		{
			int count = population.DesiredSize - population.Size;
			if (count > 0)
			{
				fillWithRobots(count, population);
			}
		}

		private void addRequiredRandomRobots(Population population)
		{
			if (settings.randomRobots > 0)
			{
				int count = (int) Math.Round(settings.randomRobots * population.DesiredSize);
				fillWithRobots(count, population);
			}
		}

		private void fillWithRobots(int count, Population population)
		{
			for (int i = 0; i < count; i++)
			{
				createNewRobot(population);
			}
		}

		private void createNewRobot(Population population)
		{
			Robot robot = Robot.createEmptyRobot(settings.dataMaximumOffset, settings.ignoreColumns);
			int instructionsCount = Math.Abs(mutator.NextInt % 1024);
			InstructionList main = robot.MainFunction;
			while (instructionsCount-- > 0)
			{
				addInstructionToMain(main);
			}
			population.saveRobot(robot);
		}

		private void addInstructionToMain(InstructionList main)
		{
			Instruction instruction = mutator.RandomInstruction;
			instruction.mutate(mutator);
			main.addInstruction(instruction);
		}

		private void breedPopulationFromParents(Population population, IList<RobotInfo> originalList)
		{
			IList<RobotInfo> robotInfos = new List<RobotInfo>(originalList);
			removeNotAllowedRobots(robotInfos);
			breedPopulationFromList(population, robotInfos);
		}

		private void removeNotAllowedRobots(IList<RobotInfo> robotInfos)
		{
			IEnumerator<RobotInfo> iterator = robotInfos.GetEnumerator();
			while (iterator.MoveNext())
			{
				RobotInfo robotInfo = iterator.Current;
				if (!robotInfo.canBeParent(settings.minimumOutcomesToAllowBreeding, settings.outcomesBetweenBreeding))
				{
//JAVA TO C# CONVERTER TODO TASK: .NET enumerators are read-only:
					iterator.remove();
				}
			}
		}

		private void breedPopulationFromList(Population population, IList<RobotInfo> list)
		{
			while (population.haveSpaceToBreed())
			{
				Robot parent1 = getPossibleParent(population, list);
				Robot parent2 = getPossibleParent(population, list);
				if (parent1 == null || parent2 == null)
				{
					break;
				}
				Robot child = Robot.createEmptyRobot(settings.dataMaximumOffset, settings.ignoreColumns);
				makeChild(parent1, parent2, child);
				population.saveRobot(child);
				parent1.increaseChildren();
				population.saveRobot(parent1);
				parent2.increaseChildren();
				population.saveRobot(parent2);
			}
		}

		private void makeChild(Robot parent1, Robot parent2, Robot child)
		{
			double weight = getParentsWeight(parent1, parent2);
			child.InheritedWeight = weight;
			InstructionList instructionList = mixMainInstructionLists(parent1, parent2);
			child.MainInstructionList = instructionList;
		}

		private double getParentsWeight(Robot parent1, Robot parent2)
		{
			return settings.inheritedWeightPercent * (parent1.Weight + parent2.Weight) / 2;
		}

		private InstructionList mixMainInstructionLists(Robot parent1, Robot parent2)
		{
			InstructionList source1 = parent1.MainFunction;
			InstructionList source2 = parent2.MainFunction;
			return blendInstructionLists(source1, source2);
		}

		/*
		This potentially will make robots gradually shorter.
		Let's say that list1.size == 4 and list2.size == 2. Average length is 3.
		Then, break1 will be between <0,3> and break2 <0,1>
		All possible lengths for new InstructionList will be: 0,1,2,3,1,2,3,4 with equal probability.
		Average length is 2.
		For higher numbers this change isn't so dramatic but may add up after many populations.
		 */
		private InstructionList blendInstructionLists(InstructionList list1, InstructionList list2)
		{
			InstructionList instructionList = InstructionList.createInstructionList();
			int break1 = getBreakPoint(list1);
			int break2 = getBreakPoint(list2);
			copyBlock(instructionList, list1, 0, break1);
			copyBlock(instructionList, list2, break2, list2.Size);
			return instructionList;
		}

		private int getBreakPoint(InstructionList list)
		{
			int size = list.Size;
			if (size == 0)
			{
				return 0;
			}
			else
			{
				return Math.Abs(mutator.NextInt % size);
			}
		}

		private void copyBlock(InstructionList destination, InstructionList source, int start, int stop)
		{
			Debug.Assert(start <= stop, "start > stop " + string.Format("{0:D} {1:D}", start, stop));
			for (int i = start; i <= stop; i++)
			{
				Instruction instruction = source.getInstruction(i).copy();
				if (instruction is TerminateInstructionList)
				{
					break;
				}
				addInstructionToInstructionList(instruction, destination);
			}
		}

		private void addInstructionToInstructionList(Instruction instruction, InstructionList instructionList)
		{
			if (mutator.skipNextInstruction())
			{
				return;
			}
			possiblyAddNewInstruction(instructionList);
			possiblyMutateInstruction(instruction);
			instructionList.addInstruction(instruction);
		}

		private void possiblyMutateInstruction(Instruction instruction)
		{
			if (mutator.AllowInstructionMutation)
			{
				instruction.mutate(mutator);
			}
		}

		private void possiblyAddNewInstruction(InstructionList instructionList)
		{
			if (mutator.AllowNewInstruction)
			{
				Instruction newInstruction = mutator.RandomInstruction;
				instructionList.addInstruction(newInstruction);
			}
		}

		private Robot getPossibleParent(Population population, IList<RobotInfo> list)
		{
			double totalWeight = sumTotalWeight(list);
			double target = Math.Abs(totalWeight * mutator.NextDouble);
			double weightSoFar = 0;
			IEnumerator<RobotInfo> iterator = list.GetEnumerator();
			while (iterator.MoveNext())
			{
				RobotInfo robotInfo = iterator.Current;
				weightSoFar += Math.Abs(robotInfo.Weight);
				if (weightSoFar >= target)
				{
//JAVA TO C# CONVERTER TODO TASK: .NET enumerators are read-only:
					iterator.remove();
					return population.getRobot(robotInfo.Name);
				}
			}
			return null;
		}

		private double sumTotalWeight(IList<RobotInfo> list)
		{
			double weight = 0;
			foreach (RobotInfo robotInfo in list)
			{
				weight += Math.Abs(robotInfo.Weight);
			}
			return weight;
		}

		public virtual void setSettings(BreederSettings breederSettings, Mutator mutator)
		{
			this.settings = breederSettings;
			this.mutator = mutator;
		}
	}

}
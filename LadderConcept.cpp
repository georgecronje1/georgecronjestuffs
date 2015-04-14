#include <stdio.h>

int ladderLength = 400;
int rungCount = 3;
int rungOffset = 25;


int main()
{
	int rungSpace = ladderLength - (rungOffset * 2);
	int rungSeparation = rungSpace / rungCount;
	int legx = 0, legY = 0, currentHeight = 0;

	for (int i = 0; i < rungCount; i++)
	{
		currentHeight = currentHeight + rungSeparation;
		printf("Create rung at X %d, Y %d and z %d \n", legx, legY, currentHeight);
	}
	
	getchar();
}
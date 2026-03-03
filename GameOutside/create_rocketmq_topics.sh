#!/bin/bash

./mqadmin updateTopic -n rocketmq-name-server:9876 -b localhost:10909 -t building-game-new-item -a +message.type=FIFO
./mqadmin updateTopic -n rocketmq-name-server:9876 -b localhost:10909 -t building-game-gift-code -a +message.type=FIFO
./mqadmin updateTopic -n rocketmq-name-server:9876 -b localhost:10909 -t building-game-mailbox-attachments -a +message.type=FIFO
./mqadmin updateTopic -n rocketmq-name-server:9876 -b localhost:10909 -t building-game-server-push -a +message.type=FIFO
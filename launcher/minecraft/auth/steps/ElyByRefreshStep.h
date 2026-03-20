// SPDX-License-Identifier: GPL-3.0-only
/*
 *  Prism Launcher - Minecraft Launcher
 *  Copyright (C) 2022
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, version 3.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 */

#pragma once

#include "minecraft/auth/AuthStep.h"
#include "net/NetJob.h"
#include "net/Upload.h"

class ElyByRefreshStep : public AuthStep {
    Q_OBJECT
   public:
    explicit ElyByRefreshStep(AccountData* data);
    QString describe() override;
    void perform() override;

   private:
    void onRequestDone(QByteArray* response);

    NetJob::Ptr m_task;
    Net::Upload::Ptr m_request;
};

import { Op } from "sequelize";
import { Template, TemplateStatus } from "../models/template.model";
import { TemplateVersion, TemplateVersionStatus } from "../models/template-version.model";
import { NotificationChannel } from "../types";

export class TemplateRepository {
  async findById(id: string): Promise<Template | null> {
    return Template.findByPk(id);
  }

  async findByKey(
    templateKey: string,
    channel: NotificationChannel,
    tenantId: string | null
  ): Promise<Template | null> {
    return Template.findOne({
      where: { templateKey, channel, tenantId },
    });
  }

  async create(input: {
    tenantId: string | null;
    templateKey: string;
    channel: NotificationChannel;
    name: string;
    description?: string | null;
    isSystemTemplate?: boolean;
  }): Promise<Template> {
    return Template.create({
      ...input,
      tenantId: input.tenantId ?? null,
      description: input.description ?? null,
      status: "active",
      isSystemTemplate: input.isSystemTemplate ?? false,
    });
  }

  async update(
    id: string,
    input: { name?: string; description?: string | null; status?: TemplateStatus }
  ): Promise<void> {
    await Template.update(input, { where: { id } });
  }

  async list(filter: {
    tenantId?: string | null;
    channel?: NotificationChannel;
    status?: TemplateStatus;
    limit?: number;
    offset?: number;
  }): Promise<{ rows: Template[]; count: number }> {
    const where: Record<string, unknown> = {};
    if (filter.tenantId !== undefined) where["tenantId"] = filter.tenantId;
    if (filter.channel) where["channel"] = filter.channel;
    if (filter.status) where["status"] = filter.status;

    const limit = Math.min(filter.limit ?? 20, 100);
    const offset = filter.offset ?? 0;

    return Template.findAndCountAll({ where, limit, offset, order: [["createdAt", "DESC"]] });
  }
}

export class TemplateVersionRepository {
  async findById(id: string): Promise<TemplateVersion | null> {
    return TemplateVersion.findByPk(id);
  }

  async findByTemplateId(templateId: string): Promise<TemplateVersion[]> {
    return TemplateVersion.findAll({
      where: { templateId },
      order: [["versionNumber", "DESC"]],
    });
  }

  async findPublishedByTemplateId(templateId: string): Promise<TemplateVersion | null> {
    return TemplateVersion.findOne({ where: { templateId, status: "published" } });
  }

  async countByTemplateId(templateId: string): Promise<number> {
    return TemplateVersion.count({ where: { templateId } });
  }

  async create(input: {
    templateId: string;
    subjectTemplate?: string | null;
    bodyTemplate: string;
    textTemplate?: string | null;
    variablesSchemaJson?: string | null;
    sampleDataJson?: string | null;
  }): Promise<TemplateVersion> {
    const count = await this.countByTemplateId(input.templateId);
    return TemplateVersion.create({
      ...input,
      versionNumber: count + 1,
      status: "draft",
      publishedAt: null,
    });
  }

  async publish(templateId: string, versionId: string): Promise<void> {
    // Retire any currently published version
    await TemplateVersion.update(
      { status: "retired" as TemplateVersionStatus },
      { where: { templateId, status: "published" } }
    );
    // Publish this version
    await TemplateVersion.update(
      { status: "published" as TemplateVersionStatus, publishedAt: new Date() },
      { where: { id: versionId, templateId } }
    );
  }
}
